//! Sequential vs Random Access Benchmark
//!
//! Demonstrates the performance impact of memory access patterns.

use criterion::{black_box, criterion_group, criterion_main, BenchmarkId, Criterion, Throughput};
use mechanical_sympathy::domain::{Order, OrderType, Side};

fn benchmark_sequential_access(c: &mut Criterion) {
    let mut group = c.benchmark_group("memory_access");

    for size in [10_000usize, 100_000, 1_000_000] {
        // Create test data
        let orders: Vec<Order> = (0..size)
            .map(|i| Order::with_id(i as u64, 1, Side::Buy, OrderType::Limit, 100.0, 100.0, 1))
            .collect();

        // Create random access indices
        let mut random_indices: Vec<usize> = (0..size).collect();
        // Simple deterministic shuffle
        for i in 0..size {
            let j = (i * 31 + 17) % size;
            random_indices.swap(i, j);
        }

        group.throughput(Throughput::Elements(size as u64));

        // Sequential access
        group.bench_with_input(
            BenchmarkId::new("sequential", size),
            &orders,
            |b, orders| {
                b.iter(|| {
                    let mut sum = 0.0;
                    for order in orders {
                        sum += order.quantity;
                    }
                    black_box(sum)
                });
            },
        );

        // Random access
        let orders_for_random = orders.clone();
        group.bench_with_input(
            BenchmarkId::new("random", size),
            &(orders_for_random, random_indices.clone()),
            |b, (orders, indices)| {
                b.iter(|| {
                    let mut sum = 0.0;
                    for &idx in indices {
                        sum += orders[idx].quantity;
                    }
                    black_box(sum)
                });
            },
        );
    }

    group.finish();
}

fn benchmark_linked_vs_array(c: &mut Criterion) {
    use mechanical_sympathy::core::sequential_buffer::LinkedNode;

    let mut group = c.benchmark_group("linked_vs_array");

    for size in [1_000usize, 10_000, 100_000] {
        group.throughput(Throughput::Elements(size as u64));

        // Array access
        let array: Vec<i64> = (0..size as i64).collect();
        group.bench_with_input(BenchmarkId::new("array", size), &array, |b, array| {
            b.iter(|| {
                let mut sum = 0i64;
                for &val in array {
                    sum += val;
                }
                black_box(sum)
            });
        });

        // Linked list (pointer chasing)
        let list = LinkedNode::create_list(size);
        group.bench_with_input(BenchmarkId::new("linked_list", size), &list, |b, list| {
            b.iter(|| black_box(LinkedNode::sum_linked(list)));
        });
    }

    group.finish();
}

criterion_group!(
    benches,
    benchmark_sequential_access,
    benchmark_linked_vs_array
);
criterion_main!(benches);
