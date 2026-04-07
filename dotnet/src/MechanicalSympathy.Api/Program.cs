using System.Diagnostics.Metrics;
using System.Threading.Channels;
using MechanicalSympathy.Api.Endpoints;
using MechanicalSympathy.Core.Infrastructure.Agents;
using MechanicalSympathy.Core.Observability;
using MechanicalSympathy.Domain.Entities;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------
// Services Configuration
// ----------------------------------------------------------------

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Create meters and metrics
var meter = new Meter("MechanicalSympathy.Api", "1.0.0");
builder.Services.AddSingleton(meter);
builder.Services.AddSingleton<TradingMetrics>();

// Trade output channel (for downstream consumers)
var tradeChannel = Channel.CreateBounded<Trade>(new BoundedChannelOptions(4096)
{
    SingleWriter = false,
    SingleReader = true,
    FullMode = BoundedChannelFullMode.Wait
});
builder.Services.AddSingleton(tradeChannel);

// Order Matching Agent (Single Writer Principle)
builder.Services.AddSingleton<OrderMatchingAgent>(sp =>
    new OrderMatchingAgent(
        tradeChannel,
        sp.GetRequiredService<Meter>(),
        sp.GetRequiredService<ILogger<OrderMatchingAgent>>(),
        capacity: 8192));

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("MechanicalSympathy.Api"))
    .WithMetrics(metrics => metrics
        .AddMeter("MechanicalSympathy.Api")
        .AddMeter("MechanicalSympathy.Trading")
        .AddRuntimeInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithTracing(tracing => tracing
        .AddSource("MechanicalSympathy.Trading")
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

// Background service to run the matching agent
builder.Services.AddHostedService<MatchingAgentHostedService>();

var app = builder.Build();

// ----------------------------------------------------------------
// Middleware Pipeline
// ----------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ----------------------------------------------------------------
// Endpoints
// ----------------------------------------------------------------

app.MapOrderEndpoints();
app.MapStatsEndpoints();
app.MapHealthEndpoints();

app.Run();

// ----------------------------------------------------------------
// Background Service for Matching Agent
// ----------------------------------------------------------------

/// <summary>
/// Hosted service that runs the OrderMatchingAgent in the background.
/// </summary>
public sealed class MatchingAgentHostedService : BackgroundService
{
    private readonly OrderMatchingAgent _agent;
    private readonly ILogger<MatchingAgentHostedService> _logger;

    public MatchingAgentHostedService(
        OrderMatchingAgent agent,
        ILogger<MatchingAgentHostedService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting OrderMatchingAgent background service");

        try
        {
            await _agent.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("OrderMatchingAgent stopped due to shutdown");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping OrderMatchingAgent");
        await _agent.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}
