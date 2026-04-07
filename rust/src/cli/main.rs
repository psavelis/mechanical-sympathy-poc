//! CLI Demo Application for Mechanical Sympathy Principles
//!
//! This application demonstrates all four mechanical sympathy principles
//! with interactive demos showing the performance impact of each technique.

use clap::{Parser, ValueEnum};
use std::time::Duration;

use mechanical_sympathy::core::{
    cache_padding::run_false_sharing_demo, sequential_buffer::run_sequential_access_demo,
};

#[derive(Parser)]
#[command(name = "ms-cli")]
#[command(about = "Mechanical Sympathy Principles Demo CLI")]
#[command(version)]
struct Cli {
    /// Which demo to run
    #[arg(short, long, value_enum, default_value = "all")]
    demo: Demo,

    /// Number of iterations for benchmarks
    #[arg(short, long, default_value = "100000000")]
    iterations: u64,
}

#[derive(Copy, Clone, PartialEq, Eq, PartialOrd, Ord, ValueEnum)]
enum Demo {
    /// Run all demos
    All,
    /// False sharing prevention demo
    FalseSharing,
    /// Single writer principle demo
    SingleWriter,
    /// Natural batching demo
    NaturalBatching,
    /// Sequential access demo
    SequentialAccess,
}

fn main() {
    let cli = Cli::parse();

    print_header();

    match cli.demo {
        Demo::All => {
            run_false_sharing_demo_cli(cli.iterations);
            println!();
            run_single_writer_demo_cli();
            println!();
            run_natural_batching_demo_cli();
            println!();
            run_sequential_access_demo_cli();
        }
        Demo::FalseSharing => run_false_sharing_demo_cli(cli.iterations),
        Demo::SingleWriter => run_single_writer_demo_cli(),
        Demo::NaturalBatching => run_natural_batching_demo_cli(),
        Demo::SequentialAccess => run_sequential_access_demo_cli(),
    }
}

fn print_header() {
    println!("╔══════════════════════════════════════════════════════════════════════╗");
    println!("║     Mechanical Sympathy Principles - Rust Implementation             ║");
    println!("║     Based on Martin Fowler's article                                 ║");
    println!("╠══════════════════════════════════════════════════════════════════════╣");
    println!(
        "║  Available parallelism: {:>4} cores                                    ║",
        std::thread::available_parallelism()
            .map(|p| p.get())
            .unwrap_or(1)
    );
    println!("║  Cache line size: 64 bytes                                           ║");
    println!("╚══════════════════════════════════════════════════════════════════════╝");
    println!();
}

fn run_false_sharing_demo_cli(iterations: u64) {
    println!("┌──────────────────────────────────────────────────────────────────────┐");
    println!("│ Demo 1: False Sharing vs Cache-Line Padding                         │");
    println!("├──────────────────────────────────────────────────────────────────────┤");
    println!("│ Two threads increment separate counters. Without padding, both      │");
    println!("│ counters share the same cache line, causing false sharing.          │");
    println!("└──────────────────────────────────────────────────────────────────────┘");
    println!();
    println!(
        "Running {} iterations per thread...",
        format_number(iterations)
    );
    println!();

    let (bad_duration, good_duration) = run_false_sharing_demo(iterations);

    println!("Results:");
    println!(
        "  BadCounter  (false sharing):     {:>10}",
        format_duration(bad_duration)
    );
    println!(
        "  GoodCounter (cache padded):      {:>10}",
        format_duration(good_duration)
    );
    println!(
        "  Speedup:                           {:>6.1}x",
        bad_duration.as_nanos() as f64 / good_duration.as_nanos() as f64
    );
}

fn run_single_writer_demo_cli() {
    println!("┌──────────────────────────────────────────────────────────────────────┐");
    println!("│ Demo 2: Single Writer Principle                                     │");
    println!("├──────────────────────────────────────────────────────────────────────┤");
    println!("│ Multiple producers send messages to a single consumer agent.        │");
    println!("│ The agent owns all mutable state - no locks required.               │");
    println!("└──────────────────────────────────────────────────────────────────────┘");
    println!();

    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        use mechanical_sympathy::core::agents::{AgentHandle, StatsAgent, StatsMessage};
        use std::time::Instant;

        const MESSAGES: u64 = 1_000_000;
        const PRODUCERS: usize = 8;

        let (handle, mut agent) = StatsAgent::new(8192);

        let start = Instant::now();

        // Spawn producers
        let mut producer_handles = Vec::new();
        for _ in 0..PRODUCERS {
            let h = handle.clone();
            producer_handles.push(tokio::spawn(async move {
                for i in 0..(MESSAGES / PRODUCERS as u64) {
                    let _ = h.send(StatsMessage(i as i64)).await;
                }
            }));
        }

        // Spawn agent
        let agent_handle = tokio::spawn(async move {
            agent.run().await;
            agent
        });

        // Wait for producers
        for h in producer_handles {
            h.await.unwrap();
        }

        // Drop the sender to close the channel
        drop(handle);

        // Wait for agent
        let agent = agent_handle.await.unwrap();
        let duration = start.elapsed();

        println!("Results:");
        println!("  Producers:                  {:>10}", PRODUCERS);
        println!(
            "  Total messages:             {:>10}",
            format_number(MESSAGES)
        );
        println!(
            "  Messages processed:         {:>10}",
            format_number(agent.count())
        );
        println!(
            "  Duration:                   {:>10}",
            format_duration(duration)
        );
        println!(
            "  Throughput:                 {:>10} msg/sec",
            format_number((agent.count() as f64 / duration.as_secs_f64()) as u64)
        );
    });
}

fn run_natural_batching_demo_cli() {
    println!("┌──────────────────────────────────────────────────────────────────────┐");
    println!("│ Demo 3: Natural Batching                                            │");
    println!("├──────────────────────────────────────────────────────────────────────┤");
    println!("│ Batches form naturally based on arrival rate, no artificial delays. │");
    println!("│ Under load, batches grow larger automatically.                      │");
    println!("└──────────────────────────────────────────────────────────────────────┘");
    println!();

    let rt = tokio::runtime::Runtime::new().unwrap();
    rt.block_on(async {
        use mechanical_sympathy::core::batching::{BatchingOptions, NaturalBatcher};
        use tokio::time::{sleep, Duration};

        let (tx, mut batcher) =
            NaturalBatcher::<i32>::with_channel(1024, BatchingOptions::with_max_size(100));

        let mut batch_sizes = Vec::new();

        // Producer that sends in bursts
        let producer = tokio::spawn(async move {
            // Burst 1: 10 items
            for i in 0..10 {
                tx.send(i).await.unwrap();
            }
            sleep(Duration::from_millis(50)).await;

            // Burst 2: 150 items (exceeds max batch size)
            for i in 10..160 {
                tx.send(i).await.unwrap();
            }
            sleep(Duration::from_millis(50)).await;

            // Burst 3: 5 items
            for i in 160..165 {
                tx.send(i).await.unwrap();
            }
        });

        // Consumer
        let consumer = tokio::spawn(async move {
            let mut sizes = Vec::new();
            while let Some(batch) = batcher.next_batch().await {
                sizes.push(batch.len());
            }
            sizes
        });

        producer.await.unwrap();
        batch_sizes = consumer.await.unwrap();

        println!("Results:");
        println!("  Max batch size configured:  {:>10}", 100);
        println!("  Batches formed:             {:>10}", batch_sizes.len());
        println!("  Batch sizes:                {:?}", batch_sizes);
        println!();
        println!("  Analysis:");
        println!("    - Small burst (10 items) → batch of ~10");
        println!("    - Large burst (150 items) → split into batches of 100, then remainder");
        println!("    - Small burst (5 items) → batch of ~5");
    });
}

fn run_sequential_access_demo_cli() {
    println!("┌──────────────────────────────────────────────────────────────────────┐");
    println!("│ Demo 4: Sequential vs Random Memory Access                          │");
    println!("├──────────────────────────────────────────────────────────────────────┤");
    println!("│ Sequential access allows CPU prefetching; random access causes      │");
    println!("│ cache misses on every access.                                       │");
    println!("└──────────────────────────────────────────────────────────────────────┘");
    println!();

    const SIZE: usize = 100_000;
    const ITERATIONS: usize = 100;

    println!(
        "Running with {} orders, {} iterations...",
        format_number(SIZE as u64),
        ITERATIONS
    );
    println!();

    let (sequential_duration, random_duration) = run_sequential_access_demo(SIZE, ITERATIONS);

    println!("Results:");
    println!(
        "  Sequential access:          {:>10}",
        format_duration(sequential_duration)
    );
    println!(
        "  Random access:              {:>10}",
        format_duration(random_duration)
    );
    println!(
        "  Speedup:                      {:>6.1}x",
        random_duration.as_nanos() as f64 / sequential_duration.as_nanos() as f64
    );
}

fn format_number(n: u64) -> String {
    let s = n.to_string();
    let mut result = String::new();
    for (i, c) in s.chars().rev().enumerate() {
        if i > 0 && i % 3 == 0 {
            result.push(',');
        }
        result.push(c);
    }
    result.chars().rev().collect()
}

fn format_duration(d: Duration) -> String {
    if d.as_secs() > 0 {
        format!("{:.2} s", d.as_secs_f64())
    } else if d.as_millis() > 0 {
        format!("{:.1} ms", d.as_secs_f64() * 1000.0)
    } else if d.as_micros() > 0 {
        format!("{:.1} µs", d.as_secs_f64() * 1_000_000.0)
    } else {
        format!("{} ns", d.as_nanos())
    }
}
