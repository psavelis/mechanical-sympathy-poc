using BenchmarkDotNet.Running;

namespace MechanicalSympathy.Benchmarks;

/// <summary>
/// BenchmarkDotNet runner for mechanical sympathy benchmarks.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks when no filter specified
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
