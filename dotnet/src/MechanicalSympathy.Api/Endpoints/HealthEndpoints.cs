using MechanicalSympathy.Core.Infrastructure.Agents;

namespace MechanicalSympathy.Api.Endpoints;

/// <summary>
/// Health check endpoints.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps health check endpoints.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/health")
            .WithTags("Health")
            .WithOpenApi();

        // GET /health - Basic health check
        group.MapGet("/", () => Results.Ok(new HealthResponse("healthy")))
            .WithName("HealthCheck")
            .WithSummary("Basic health check")
            .Produces<HealthResponse>();

        // GET /health/ready - Readiness check
        group.MapGet("/ready", (OrderMatchingAgent agent) =>
        {
            // Check if matching agent is operational
            var isReady = agent.PendingCount < 10000; // Not overwhelmed

            if (isReady)
            {
                return Results.Ok(new ReadinessResponse(true, "Ready to accept orders"));
            }

            return Results.Json(
                new ReadinessResponse(false, "Matching engine overloaded"),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("ReadinessCheck")
        .WithSummary("Readiness check")
        .WithDescription("Returns whether the service is ready to accept traffic")
        .Produces<ReadinessResponse>()
        .Produces<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable);

        // GET /health/live - Liveness check
        group.MapGet("/live", () => Results.Ok(new HealthResponse("alive")))
            .WithName("LivenessCheck")
            .WithSummary("Liveness check")
            .WithDescription("Returns whether the service is alive (for Kubernetes probes)")
            .Produces<HealthResponse>();
    }
}

/// <summary>
/// Basic health response.
/// </summary>
public record HealthResponse(string Status);

/// <summary>
/// Readiness response with details.
/// </summary>
public record ReadinessResponse(bool Ready, string Message);
