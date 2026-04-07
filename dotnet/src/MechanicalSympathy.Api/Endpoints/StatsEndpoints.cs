using MechanicalSympathy.Core.Infrastructure.Agents;

namespace MechanicalSympathy.Api.Endpoints;

/// <summary>
/// Statistics and monitoring endpoints.
/// </summary>
public static class StatsEndpoints
{
    /// <summary>
    /// Maps statistics-related endpoints.
    /// </summary>
    public static void MapStatsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stats")
            .WithTags("Statistics")
            .WithOpenApi();

        // GET /api/stats - Get current trading statistics
        group.MapGet("/", (OrderMatchingAgent agent) =>
        {
            return Results.Ok(new TradingStats(
                TotalOrdersProcessed: agent.TotalOrdersProcessed,
                TotalTradesExecuted: agent.TotalTradesExecuted,
                PendingOrders: agent.PendingCount,
                ProcessorCount: Environment.ProcessorCount
            ));
        })
        .WithName("GetStats")
        .WithSummary("Get trading statistics")
        .WithDescription("Returns current trading engine statistics including orders processed and trades executed")
        .Produces<TradingStats>();

        // GET /api/stats/system - Get system information
        group.MapGet("/system", () =>
        {
            return Results.Ok(new SystemInfo(
                ProcessorCount: Environment.ProcessorCount,
                Is64BitProcess: Environment.Is64BitProcess,
                Is64BitOS: Environment.Is64BitOperatingSystem,
                OSDescription: System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                FrameworkDescription: System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                ProcessArchitecture: System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                GCIsServerGC: System.Runtime.GCSettings.IsServerGC,
                GCLatencyMode: System.Runtime.GCSettings.LatencyMode.ToString(),
                WorkingSet64MB: Environment.WorkingSet / (1024 * 1024)
            ));
        })
        .WithName("GetSystemInfo")
        .WithSummary("Get system information")
        .WithDescription("Returns information about the runtime environment")
        .Produces<SystemInfo>();
    }
}

/// <summary>
/// Trading statistics.
/// </summary>
public record TradingStats(
    long TotalOrdersProcessed,
    long TotalTradesExecuted,
    int PendingOrders,
    int ProcessorCount
);

/// <summary>
/// System information.
/// </summary>
public record SystemInfo(
    int ProcessorCount,
    bool Is64BitProcess,
    bool Is64BitOS,
    string OSDescription,
    string FrameworkDescription,
    string ProcessArchitecture,
    bool GCIsServerGC,
    string GCLatencyMode,
    long WorkingSet64MB
);
