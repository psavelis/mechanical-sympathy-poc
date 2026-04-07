namespace MechanicalSympathy.Core.Infrastructure.CachePadding;

/// <summary>
/// Cache line constants for x86/x64/ARM64 architectures.
/// Cache lines are the smallest unit of data that can be transferred
/// between the CPU cache and main memory.
/// </summary>
/// <remarks>
/// <para>
/// Modern CPUs use cache lines of 64 bytes. When two threads access
/// different variables that happen to be on the same cache line,
/// the cache coherency protocol forces unnecessary synchronization
/// between CPU cores - this is called "false sharing".
/// </para>
/// <para>
/// By padding data structures to occupy full cache lines, we prevent
/// false sharing and allow truly independent parallel access.
/// </para>
/// </remarks>
public static class CacheLineConstants
{
    /// <summary>
    /// Standard cache line size on modern x86/x64 and ARM64 processors.
    /// This is 64 bytes on most current hardware.
    /// </summary>
    public const int CacheLineSize = 64;

    /// <summary>
    /// Padding size needed after a long (8 bytes) to fill a cache line.
    /// Used when manually padding structures.
    /// </summary>
    public const int PaddingAfterLong = CacheLineSize - sizeof(long);

    /// <summary>
    /// Padding size needed after an int (4 bytes) to fill a cache line.
    /// </summary>
    public const int PaddingAfterInt = CacheLineSize - sizeof(int);

    /// <summary>
    /// Padding size needed after a double (8 bytes) to fill a cache line.
    /// </summary>
    public const int PaddingAfterDouble = CacheLineSize - sizeof(double);
}
