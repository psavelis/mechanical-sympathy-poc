//! False Sharing Benchmark
//!
//! Compares performance of counters with and without cache padding.

use criterion::{black_box, criterion_group, criterion_main, BenchmarkId, Criterion, Throughput};
use mechanical_sympathy::core::cache_padding::{BadCounter, GoodCounter};
use std::sync::Arc;
use std::thread;

fn benchmark_false_sharing(c: &mut Criterion) {
    let mut group = c.benchmark_group("false_sharing");

    for iterations in [1_000_000u64, 10_000_000, 100_000_000] {
        group.throughput(Throughput::Elements(iterations * 2)); // Two counters

        group.bench_with_input(
            BenchmarkId::new("bad_counter", iterations),
            &iterations,
            |b, &iterations| {
                b.iter(|| {
                    let counter = Arc::new(BadCounter::new());
                    let c1 = Arc::clone(&counter);
                    let c2 = Arc::clone(&counter);

                    let h1 = thread::spawn(move || {
                        for _ in 0..iterations {
                            c1.increment_count1();
                        }
                    });

                    let h2 = thread::spawn(move || {
                        for _ in 0..iterations {
                            c2.increment_count2();
                        }
                    });

                    h1.join().unwrap();
                    h2.join().unwrap();

                    black_box(counter.get_count1() + counter.get_count2())
                });
            },
        );

        group.bench_with_input(
            BenchmarkId::new("good_counter", iterations),
            &iterations,
            |b, &iterations| {
                b.iter(|| {
                    let counter = Arc::new(GoodCounter::new());
                    let c1 = Arc::clone(&counter);
                    let c2 = Arc::clone(&counter);

                    let h1 = thread::spawn(move || {
                        for _ in 0..iterations {
                            c1.increment_count1();
                        }
                    });

                    let h2 = thread::spawn(move || {
                        for _ in 0..iterations {
                            c2.increment_count2();
                        }
                    });

                    h1.join().unwrap();
                    h2.join().unwrap();

                    black_box(counter.get_count1() + counter.get_count2())
                });
            },
        );
    }

    group.finish();
}

criterion_group!(benches, benchmark_false_sharing);
criterion_main!(benches);
