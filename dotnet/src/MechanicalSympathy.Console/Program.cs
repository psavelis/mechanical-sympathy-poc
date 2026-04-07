using MechanicalSympathy.Console.Demos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ----------------------------------------------------------------
// Mechanical Sympathy Principles - .NET 9 PoC
// Based on Martin Fowler's article
// ----------------------------------------------------------------

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register demo services
builder.Services.AddTransient<FalseSharingDemo>();
builder.Services.AddTransient<SingleWriterDemo>();
builder.Services.AddTransient<NaturalBatchingDemo>();
builder.Services.AddTransient<SequentialAccessDemo>();

var host = builder.Build();

// Print header
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     Mechanical Sympathy Principles - .NET 9 Production PoC           ║");
Console.WriteLine("║     Based on Martin Fowler's article                                 ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Environment.ProcessorCount = {Environment.ProcessorCount,-4} cores                             ║");
Console.WriteLine($"║  Server GC: {(System.Runtime.GCSettings.IsServerGC ? "Yes" : "No "),-3}                                                       ║");
Console.WriteLine($"║  64-bit Process: {(Environment.Is64BitProcess ? "Yes" : "No "),-3}                                                  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Parse command line arguments
var demoArg = args.FirstOrDefault()?.ToLowerInvariant();

if (demoArg == "--help" || demoArg == "-h")
{
    PrintHelp();
    return;
}

var demoName = demoArg switch
{
    "--demo" or "-d" => args.Skip(1).FirstOrDefault()?.ToLowerInvariant() ?? "all",
    null => "all",
    _ => demoArg.StartsWith('-') ? "all" : demoArg
};

await RunDemoAsync(host, demoName);

// Print summary
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                         Key Takeaways                                ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║ 1. Cache Line Padding: Prevents false sharing (3-10x speedup)       ║");
Console.WriteLine("║ 2. Single Writer: One thread owns all writes = zero contention      ║");
Console.WriteLine("║ 3. Natural Batching: Begin immediately, complete on queue empty     ║");
Console.WriteLine("║ 4. Sequential Access: Linear iteration maximizes cache efficiency   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

static void PrintHelp()
{
    Console.WriteLine("Usage: MechanicalSympathy.Console [OPTIONS] [DEMO]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -h, --help       Show this help message");
    Console.WriteLine("  -d, --demo NAME  Run a specific demo");
    Console.WriteLine();
    Console.WriteLine("Demos:");
    Console.WriteLine("  all              Run all demos (default)");
    Console.WriteLine("  false-sharing    Cache line padding demonstration");
    Console.WriteLine("  single-writer    Single Writer Principle with channels");
    Console.WriteLine("  batching         Natural Batching pattern");
    Console.WriteLine("  sequential       Sequential memory access patterns");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run                          # Run all demos");
    Console.WriteLine("  dotnet run --demo false-sharing     # Run false sharing demo only");
    Console.WriteLine("  dotnet run -d single-writer         # Run single writer demo only");
}

static async Task RunDemoAsync(IHost host, string demoName)
{
    switch (demoName)
    {
        case "false-sharing":
            await host.Services.GetRequiredService<FalseSharingDemo>().RunAsync();
            break;

        case "single-writer":
            await host.Services.GetRequiredService<SingleWriterDemo>().RunAsync();
            break;

        case "batching":
            await host.Services.GetRequiredService<NaturalBatchingDemo>().RunAsync();
            break;

        case "sequential":
            await host.Services.GetRequiredService<SequentialAccessDemo>().RunAsync();
            break;

        case "all":
        default:
            await host.Services.GetRequiredService<FalseSharingDemo>().RunAsync();
            Console.WriteLine();
            await host.Services.GetRequiredService<SingleWriterDemo>().RunAsync();
            Console.WriteLine();
            await host.Services.GetRequiredService<NaturalBatchingDemo>().RunAsync();
            Console.WriteLine();
            await host.Services.GetRequiredService<SequentialAccessDemo>().RunAsync();
            break;
    }
}
