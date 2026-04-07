using MechanicalSympathy.Core.Infrastructure.Agents;
using MechanicalSympathy.Core.Observability;
using MechanicalSympathy.Domain.Entities;
using MechanicalSympathy.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace MechanicalSympathy.Api.Endpoints;

/// <summary>
/// Order management endpoints.
/// </summary>
public static class OrderEndpoints
{
    private static long _nextOrderId = 1;

    /// <summary>
    /// Maps order-related endpoints.
    /// </summary>
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .WithOpenApi();

        // POST /api/orders - Place a new order
        group.MapPost("/", async (
            [FromBody] PlaceOrderRequest request,
            OrderMatchingAgent agent,
            TradingMetrics metrics) =>
        {
            // Validate request
            if (request.Quantity <= 0)
                return Results.BadRequest(new { error = "Quantity must be positive" });

            if (request.Type == OrderType.Limit && request.Price <= 0)
                return Results.BadRequest(new { error = "Price must be positive for limit orders" });

            // Create order
            var orderId = Interlocked.Increment(ref _nextOrderId);
            var order = Order.Create(
                id: orderId,
                instrumentId: request.InstrumentId,
                side: request.Side,
                type: request.Type,
                price: request.Price,
                quantity: request.Quantity,
                clientId: request.ClientId,
                clientOrderId: request.ClientOrderId
            );

            // Send to matching agent (Single Writer Principle)
            await agent.SendAsync(new PlaceOrderCommand(order));

            metrics.RecordOrderReceived();

            return Results.Accepted($"/api/orders/{orderId}", new PlaceOrderResponse(orderId));
        })
        .WithName("PlaceOrder")
        .WithSummary("Place a new order")
        .WithDescription("Submits a new order to the matching engine using the Single Writer Principle")
        .Produces<PlaceOrderResponse>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // DELETE /api/orders/{orderId} - Cancel an order
        group.MapDelete("/{orderId:long}", async (
            long orderId,
            [FromQuery] long instrumentId,
            OrderMatchingAgent agent) =>
        {
            await agent.SendAsync(new CancelOrderCommand(orderId, instrumentId));
            return Results.Accepted();
        })
        .WithName("CancelOrder")
        .WithSummary("Cancel an order")
        .WithDescription("Sends a cancellation request to the matching engine")
        .Produces(StatusCodes.Status202Accepted);

        // GET /api/orders/book/{instrumentId} - Get order book snapshot
        group.MapGet("/book/{instrumentId:long}", (
            long instrumentId,
            OrderMatchingAgent agent) =>
        {
            var snapshot = agent.GetOrderBookSnapshot(instrumentId);
            if (snapshot == null)
                return Results.NotFound(new { error = $"Order book not found for instrument {instrumentId}" });

            return Results.Ok(snapshot);
        })
        .WithName("GetOrderBook")
        .WithSummary("Get order book snapshot")
        .WithDescription("Returns a snapshot of the current order book state")
        .Produces<OrderBookSnapshot>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>
/// Request to place a new order.
/// </summary>
public record PlaceOrderRequest(
    long InstrumentId,
    Side Side,
    OrderType Type,
    decimal Price,
    decimal Quantity,
    long ClientId = 1,
    string? ClientOrderId = null
);

/// <summary>
/// Response after placing an order.
/// </summary>
public record PlaceOrderResponse(long OrderId);
