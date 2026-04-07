using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;

namespace MechanicalSympathy.Benchmarks.Configs;

/// <summary>
/// Custom benchmark configuration for mechanical sympathy benchmarks.
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        // .NET 9 with Server GC (production-like settings)
        AddJob(Job.Default
            .WithId("net9-ServerGC")
            .WithRuntime(CoreRuntime.Core90)
            .WithGcServer(true)
            .WithWarmupCount(3)
            .WithIterationCount(10));

        // Memory diagnoser for allocation tracking
        AddDiagnoser(MemoryDiagnoser.Default);

        // Export to multiple formats
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);

        // Show meaningful columns
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(StatisticColumn.Median);
        AddColumn(StatisticColumn.P95);
        AddColumn(RankColumn.Arabic);
    }
}

/// <summary>
/// Quick benchmark configuration for faster iteration during development.
/// </summary>
public class QuickBenchmarkConfig : ManualConfig
{
    public QuickBenchmarkConfig()
    {
        AddJob(Job.Default
            .WithId("quick")
            .WithRuntime(CoreRuntime.Core90)
            .WithGcServer(true)
            .WithWarmupCount(1)
            .WithIterationCount(3));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(RankColumn.Arabic);
    }
}
