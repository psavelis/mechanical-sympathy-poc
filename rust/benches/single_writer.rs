//! Single Writer Principle Benchmark
//!
//! Measures throughput of the agent-based message processing pattern.

use criterion::{black_box, criterion_group, criterion_main, BenchmarkId, Criterion, Throughput};
use mechanical_sympathy::core::agents::{AgentHandle, StatsAgent, StatsMessage};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;
use tokio::runtime::Runtime;

fn benchmark_single_writer(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();
    let mut group = c.benchmark_group("single_writer");

    for (messages, producers) in [
        (100_000u64, 1usize),
        (100_000, 4),
        (100_000, 8),
        (1_000_000, 8),
    ] {
        let id = format!("{}msg_{}prod", messages, producers);
        group.throughput(Throughput::Elements(messages));

        group.bench_with_input(
            BenchmarkId::new("agent", &id),
            &(messages, producers),
            |b, &(messages, producers)| {
                b.iter(|| {
                    rt.block_on(async {
                        let (handle, mut agent) = StatsAgent::new(8192);

                        // Spawn agent
                        let agent_handle = tokio::spawn(async move {
                            agent.run().await;
                            agent
                        });

                        // Spawn producers
                        let msgs_per_producer = messages / producers as u64;
                        let mut producer_handles = Vec::new();

                        for _ in 0..producers {
                            let h = handle.clone();
                            producer_handles.push(tokio::spawn(async move {
                                for i in 0..msgs_per_producer {
                                    let _ = h.send(StatsMessage(i as i64)).await;
                                }
                            }));
                        }

                        // Wait for producers
                        for h in producer_handles {
                            h.await.unwrap();
                        }

                        // Close channel
                        drop(handle);

                        // Wait for agent
                        let agent = agent_handle.await.unwrap();
                        black_box(agent.total())
                    })
                });
            },
        );

        // Compare with mutex-based approach
        group.bench_with_input(
            BenchmarkId::new("mutex", &id),
            &(messages, producers),
            |b, &(messages, producers)| {
                b.iter(|| {
                    rt.block_on(async {
                        let counter = Arc::new(tokio::sync::Mutex::new(0i64));
                        let msgs_per_producer = messages / producers as u64;
                        let mut handles = Vec::new();

                        for _ in 0..producers {
                            let c = Arc::clone(&counter);
                            handles.push(tokio::spawn(async move {
                                for i in 0..msgs_per_producer {
                                    let mut guard = c.lock().await;
                                    *guard += i as i64;
                                }
                            }));
                        }

                        for h in handles {
                            h.await.unwrap();
                        }

                        let result = *counter.lock().await;
                        black_box(result)
                    })
                });
            },
        );
    }

    group.finish();
}

criterion_group!(benches, benchmark_single_writer);
criterion_main!(benches);
