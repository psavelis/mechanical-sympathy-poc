//! Axum HTTP API for Mechanical Sympathy PoC
//!
//! Provides REST endpoints for:
//! - Order submission and management
//! - Statistics and metrics
//! - Health checks

use axum::{
    extract::{Path, State},
    http::StatusCode,
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use mechanical_sympathy::{
    core::agents::{OrderCommand, OrderMatchingAgent},
    domain::{Order, OrderType, Side, Trade},
};
use serde::{Deserialize, Serialize};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;
use tokio::sync::{mpsc, oneshot};
use tower_http::trace::TraceLayer;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};

/// Application state shared across handlers
#[derive(Clone)]
struct AppState {
    order_sender: mpsc::Sender<OrderCommand>,
    trade_receiver: Arc<tokio::sync::Mutex<mpsc::Receiver<Trade>>>,
    total_orders: Arc<AtomicU64>,
    total_trades: Arc<AtomicU64>,
}

/// Request to place an order
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct PlaceOrderRequest {
    instrument_id: u64,
    side: String,
    #[serde(default)]
    order_type: Option<String>,
    price: f64,
    quantity: f64,
    #[serde(default)]
    client_id: Option<u64>,
}

/// Response for order placement
#[derive(Debug, Serialize)]
struct PlaceOrderResponse {
    id: u64,
    status: String,
}

/// Health check response
#[derive(Debug, Serialize)]
struct HealthResponse {
    status: String,
}

/// Readiness check response
#[derive(Debug, Serialize)]
struct ReadinessResponse {
    ready: bool,
    message: String,
}

/// Stats response
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct StatsResponse {
    total_orders_processed: u64,
    total_trades_executed: u64,
}

#[tokio::main]
async fn main() {
    // Initialize tracing
    tracing_subscriber::registry()
        .with(tracing_subscriber::fmt::layer())
        .with(tracing_subscriber::EnvFilter::from_default_env())
        .init();

    // Create trade channel
    let (trade_tx, trade_rx) = mpsc::channel::<Trade>(10000);

    // Create order matching agent
    let (order_handle, mut order_agent) = OrderMatchingAgent::new(8192, trade_tx);
    let total_orders = order_agent.total_orders_counter();
    let total_trades = order_agent.total_trades_counter();

    // Spawn agent
    tokio::spawn(async move {
        order_agent.run().await;
    });

    // Create app state - use the sender from the handle
    let state = AppState {
        order_sender: order_handle.sender_clone(),
        trade_receiver: Arc::new(tokio::sync::Mutex::new(trade_rx)),
        total_orders,
        total_trades,
    };

    // Build router
    let app = Router::new()
        // Health endpoints
        .route("/health", get(health_check))
        .route("/health/ready", get(readiness_check))
        .route("/health/live", get(liveness_check))
        // Order endpoints
        .route("/api/orders", post(place_order))
        .route("/api/orders/stats", get(get_stats))
        .route("/api/orderbook/:instrument_id", get(get_orderbook))
        // Add state and tracing
        .with_state(state)
        .layer(TraceLayer::new_for_http());

    // Get port from environment or default
    let port = std::env::var("PORT").unwrap_or_else(|_| "8080".to_string());
    let addr = format!("0.0.0.0:{}", port);

    tracing::info!("Starting server on {}", addr);

    let listener = tokio::net::TcpListener::bind(&addr).await.unwrap();
    axum::serve(listener, app).await.unwrap();
}

/// Health check endpoint
async fn health_check() -> Json<HealthResponse> {
    Json(HealthResponse {
        status: "healthy".to_string(),
    })
}

/// Readiness check endpoint
async fn readiness_check(State(state): State<AppState>) -> impl IntoResponse {
    let total = state.total_orders.load(Ordering::Relaxed);
    let is_ready = total < 1_000_000; // Not overwhelmed

    if is_ready {
        (
            StatusCode::OK,
            Json(ReadinessResponse {
                ready: true,
                message: "Ready to accept orders".to_string(),
            }),
        )
    } else {
        (
            StatusCode::SERVICE_UNAVAILABLE,
            Json(ReadinessResponse {
                ready: false,
                message: "Matching engine overloaded".to_string(),
            }),
        )
    }
}

/// Liveness check endpoint
async fn liveness_check() -> Json<HealthResponse> {
    Json(HealthResponse {
        status: "alive".to_string(),
    })
}

/// Place an order
async fn place_order(
    State(state): State<AppState>,
    Json(request): Json<PlaceOrderRequest>,
) -> impl IntoResponse {
    let side = match request.side.to_lowercase().as_str() {
        "buy" => Side::Buy,
        "sell" => Side::Sell,
        _ => {
            return (
                StatusCode::BAD_REQUEST,
                Json(serde_json::json!({"error": "Invalid side. Must be 'buy' or 'sell'"})),
            )
                .into_response()
        }
    };

    let order_type = match request.order_type.as_deref().unwrap_or("limit").to_lowercase().as_str()
    {
        "limit" => OrderType::Limit,
        "market" => OrderType::Market,
        _ => OrderType::Limit,
    };

    let order = Order::new(
        request.instrument_id,
        side,
        order_type,
        request.price,
        request.quantity,
        request.client_id.unwrap_or(0),
    );

    let order_id = order.id;

    if let Err(_) = state
        .order_sender
        .send(OrderCommand::PlaceOrder(order))
        .await
    {
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(serde_json::json!({"error": "Failed to submit order"})),
        )
            .into_response();
    }

    (
        StatusCode::ACCEPTED,
        Json(PlaceOrderResponse {
            id: order_id,
            status: "accepted".to_string(),
        }),
    )
        .into_response()
}

/// Get order processing statistics
async fn get_stats(State(state): State<AppState>) -> Json<StatsResponse> {
    Json(StatsResponse {
        total_orders_processed: state.total_orders.load(Ordering::Relaxed),
        total_trades_executed: state.total_trades.load(Ordering::Relaxed),
    })
}

/// Get order book snapshot
async fn get_orderbook(
    State(state): State<AppState>,
    Path(instrument_id): Path<u64>,
) -> impl IntoResponse {
    let (tx, rx) = oneshot::channel();

    if let Err(_) = state
        .order_sender
        .send(OrderCommand::GetSnapshot {
            instrument_id,
            reply: tx,
        })
        .await
    {
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(serde_json::json!({"error": "Failed to get orderbook"})),
        )
            .into_response();
    }

    match rx.await {
        Ok(Some(snapshot)) => (
            StatusCode::OK,
            Json(serde_json::json!({
                "instrumentId": snapshot.instrument_id,
                "bestBid": snapshot.best_bid,
                "bestAsk": snapshot.best_ask,
                "spread": snapshot.spread,
                "totalBidOrders": snapshot.total_bid_orders,
                "totalAskOrders": snapshot.total_ask_orders
            })),
        )
            .into_response(),
        Ok(None) => (
            StatusCode::NOT_FOUND,
            Json(serde_json::json!({"error": "Order book not found"})),
        )
            .into_response(),
        Err(_) => (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(serde_json::json!({"error": "Failed to get response"})),
        )
            .into_response(),
    }
}
