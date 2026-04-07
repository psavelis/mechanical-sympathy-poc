# Mechanical Sympathy PoC

Proof of Concept demonstrating mechanical sympathy principles from [Martin Fowler's article](https://martinfowler.com/articles/mechanical-sympathy-principles.html), applied to a low-latency order processing system.

**Multi-language implementation** comparing **.NET 9**, **Rust**, and **Go 1.23** to demonstrate the universal effectiveness of these techniques.

[![CI](https://github.com/psavelis/mechanical-sympathy-poc/actions/workflows/ci.yml/badge.svg)](https://github.com/psavelis/mechanical-sympathy-poc/actions/workflows/ci.yml)
[![Benchmarks](https://github.com/psavelis/mechanical-sympathy-poc/actions/workflows/benchmarks.yml/badge.svg)](https://github.com/psavelis/mechanical-sympathy-poc/actions/workflows/benchmarks.yml)

## Key Principles Demonstrated

| Principle | Description | .NET Implementation | Rust Implementation | Go Implementation |
|-----------|-------------|---------------------|---------------------|-------------------|
| **Cache Line Padding** | 64-byte padding to prevent false sharing | `StructLayout` with explicit offsets | `cache-padded` crate | Byte array padding |
| **Single Writer Principle** | One writer per mutable state, message-passing for modifications | `System.Threading.Channels` | `tokio::sync::mpsc` | Native channels |
| **Natural Batching** | Process batches when available, don't wait artificially | Custom batch processor | Custom batch processor | Select with default |
| **Sequential Memory Access** | Linear memory traversal for CPU prefetching | Array-based buffer | Vec-based buffer | Slice-based buffer |

## Quick Start

### Using Docker (Recommended for Fair Comparison)

```bash
cd docker

# Run all demos
docker-compose --profile demo up

# Run .NET demo
docker-compose run dotnet-console --demo false-sharing

# Run Rust demo
docker-compose run rust-cli --demo false-sharing

# Run Go demo
docker-compose run go-cli --demo false-sharing

# Run benchmarks (4 CPUs, 4GB RAM constraints)
docker-compose --profile benchmarks up

# Start APIs
docker-compose --profile api up
# .NET API: http://localhost:8081
# Rust API: http://localhost:8082
# Go API: http://localhost:8083
```

### Local Development

#### .NET 9

```bash
cd dotnet

# Build and test
dotnet build -c Release
dotnet test -c Release

# Run demos
dotnet run --project src/MechanicalSympathy.Console -c Release

# Run API
dotnet run --project src/MechanicalSympathy.Api -c Release

# Run benchmarks
dotnet run --project benchmarks/MechanicalSympathy.Benchmarks -c Release
```

#### Rust

```bash
cd rust

# Build and test
cargo build --release
cargo test --release

# Run demos
cargo run --bin ms-cli --release -- --demo false-sharing

# Run API
cargo run --bin ms-api --release

# Run benchmarks
cargo bench
```

#### Go 1.23

```bash
cd go

# Build and test
go build ./...
go test ./...

# Run demos
go run ./cmd/cli --demo false-sharing

# Run API
go run ./cmd/api

# Run benchmarks
go test -bench=. -benchmem ./benchmarks/...
```

## Running Benchmarks

```bash
# Run all benchmarks and generate report
./scripts/run-benchmarks.sh

# Run only .NET benchmarks
./scripts/run-benchmarks.sh --dotnet-only

# Run only Rust benchmarks
./scripts/run-benchmarks.sh --rust-only

# Run only Go benchmarks
./scripts/run-benchmarks.sh --go-only

# Run in Docker (fair comparison with resource constraints)
./scripts/run-benchmarks.sh --docker
```

Benchmark results are automatically posted to PRs and saved to [`docs/benchmark.md`](docs/benchmark.md).

## API Endpoints

Both APIs expose identical endpoints:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/health/ready` | GET | Readiness probe |
| `/health/live` | GET | Liveness probe |
| `/api/orders` | POST | Place an order |
| `/api/orders/stats` | GET | Get processing statistics |
| `/api/orderbook/:id` | GET | Get order book snapshot |

### Example

```bash
# Place an order (.NET on 8081, Rust on 8082, Go on 8083)
curl -X POST http://localhost:8081/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "instrumentId": 1,
    "side": "buy",
    "orderType": "limit",
    "price": 100.50,
    "quantity": 100
  }'

# Get stats
curl http://localhost:8081/api/orders/stats

# Get order book
curl http://localhost:8081/api/orderbook/1
```

## Project Structure

```
.
├── dotnet/                          # .NET 9 implementation
│   ├── src/
│   │   ├── MechanicalSympathy.Domain/    # Domain entities
│   │   ├── MechanicalSympathy.Core/      # Core implementations
│   │   │   └── Infrastructure/
│   │   │       ├── CachePadding/         # False sharing prevention
│   │   │       ├── Agents/               # Single Writer agents
│   │   │       ├── Batching/             # Natural batching
│   │   │       └── Memory/               # Sequential access buffers
│   │   ├── MechanicalSympathy.Api/       # ASP.NET Core API
│   │   └── MechanicalSympathy.Console/   # Demo application
│   ├── tests/
│   └── benchmarks/
│
├── rust/                            # Rust implementation
│   ├── src/
│   │   ├── domain/                       # Domain entities
│   │   │   ├── order.rs
│   │   │   ├── order_book.rs
│   │   │   ├── price_level.rs
│   │   │   └── trade.rs
│   │   ├── core/                         # Core implementations
│   │   │   ├── agents.rs                 # Single Writer agents
│   │   │   ├── batching.rs               # Natural batching
│   │   │   ├── cache_padding.rs          # False sharing prevention
│   │   │   └── sequential_buffer.rs      # Sequential access
│   │   ├── api/                          # Axum HTTP API
│   │   └── cli/                          # Demo application
│   └── benches/                          # Criterion benchmarks
│
├── go/                              # Go 1.23 implementation
│   ├── cmd/
│   │   ├── api/                          # net/http API
│   │   └── cli/                          # Demo application
│   ├── internal/
│   │   ├── domain/                       # Domain entities
│   │   └── core/                         # Core implementations
│   │       ├── agents/                   # Single Writer agents
│   │       ├── batching/                 # Natural batching
│   │       ├── cachepadding/             # False sharing prevention
│   │       └── memory/                   # Sequential access
│   ├── benchmarks/                       # Go benchmarks
│   └── tests/                            # Unit tests
│
├── docker/                          # Docker configuration
│   ├── dotnet/
│   ├── rust/
│   ├── go/
│   └── docker-compose.yml
│
├── scripts/
│   └── run-benchmarks.sh            # Benchmark runner
│
├── docs/
│   └── benchmark.md                 # Generated benchmark report
│
└── .github/workflows/
    ├── ci.yml                       # CI pipeline
    └── benchmarks.yml               # Benchmark pipeline with PR reporting
```

## Expected Results

| Benchmark | Naive | Optimized | Typical Speedup |
|-----------|-------|-----------|-----------------|
| False Sharing | ~2000ms | ~400ms | 3-10x |
| Single Writer (8 producers) | High contention | Linear scale | 2-5x |
| Sequential vs Random Access | 100% | 20-40% | 2-5x |
| Batched vs Individual | O(n) overhead | O(1) amortized | 2-5x |

See [`docs/benchmark.md`](docs/benchmark.md) for detailed benchmark results.

## CI/CD

- **CI Pipeline**: Builds and tests both implementations on every push/PR
- **Benchmark Pipeline**: Runs full benchmark suite and posts comparison report to PRs
- **Docker Builds**: Validates containerized builds for consistent benchmarking

## Requirements

| Component | .NET | Rust | Go |
|-----------|------|------|-----|
| SDK/Toolchain | .NET 9.0 SDK | Rust 1.85+ | Go 1.23+ |
| Docker | Optional | Optional | Optional |
| OS | Linux, macOS, Windows | Linux, macOS, Windows | Linux, macOS, Windows |

## References

- [Mechanical Sympathy Principles - Martin Fowler](https://martinfowler.com/articles/mechanical-sympathy-principles.html)
- [What is Mechanical Sympathy? - Martin Thompson](https://mechanical-sympathy.blogspot.com/)
- [LMAX Disruptor](https://lmax-exchange.github.io/disruptor/)

## License

MIT
