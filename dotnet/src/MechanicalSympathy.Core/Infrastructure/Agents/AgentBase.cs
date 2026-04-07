using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Core.Infrastructure.Agents;

/// <summary>
/// Base class implementing Single Writer Principle using System.Threading.Channels.
/// </summary>
/// <remarks>
/// <para>
/// This is the core implementation of the Single Writer Principle from Martin Fowler's
/// mechanical sympathy article. Key properties:
/// </para>
/// <list type="bullet">
/// <item>
/// <term>SingleReader=true</term>
/// <description>Only one thread consumes messages (the agent thread)</description>
/// </item>
/// <item>
/// <term>BoundedChannelFullMode.Wait</term>
/// <description>Provides backpressure when queue is full</description>
/// </item>
/// <item>
/// <term>All state mutations on single thread</term>
/// <description>No locks needed, no race conditions possible</description>
/// </item>
/// </list>
/// <para>
/// Benefits of this pattern:
/// </para>
/// <list type="bullet">
/// <item>Zero lock contention - state is only modified by one thread</item>
/// <item>Eliminates race conditions by design</item>
/// <item>No context switching overhead from mutex waits</item>
/// <item>Enables natural batching (processing multiple messages per wake)</item>
/// </list>
/// </remarks>
/// <typeparam name="TMessage">The type of messages this agent processes.</typeparam>
public abstract class AgentBase<TMessage> : IAgent<TMessage>
{
    private readonly Channel<TMessage> _channel;
    private readonly ILogger _logger;
    private readonly string _agentName;
    private long _messagesProcessed;

    /// <summary>
    /// Gets the total number of messages processed by this agent.
    /// </summary>
    public long MessagesProcessed => Interlocked.Read(ref _messagesProcessed);

    /// <summary>
    /// Gets the number of messages currently pending in the queue.
    /// </summary>
    public int PendingCount => _channel.Reader.Count;

    /// <summary>
    /// Creates a new agent with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of messages to buffer.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="agentName">Optional name for logging.</param>
    protected AgentBase(
        int capacity,
        ILogger logger,
        string? agentName = null)
    {
        _logger = logger;
        _agentName = agentName ?? GetType().Name;

        _channel = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(capacity)
        {
            // Multiple producers can send messages concurrently
            SingleWriter = false,

            // SINGLE WRITER PRINCIPLE: Only this agent reads and processes messages
            // This is the key setting that enables lock-free state management
            SingleReader = true,

            // Backpressure: producers wait when channel is full
            FullMode = BoundedChannelFullMode.Wait,

            // Avoid potential stack overflows in synchronous code paths
            AllowSynchronousContinuations = false
        });
    }

    /// <summary>
    /// Sends a message to the agent for processing.
    /// </summary>
    public ValueTask SendAsync(TMessage message, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    /// <summary>
    /// Starts the agent's processing loop. This method runs until cancelled or stopped.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent {AgentName} starting", _agentName);

        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    // SINGLE WRITER: All state mutations happen here, on this single thread
                    await ProcessMessageAsync(message, cancellationToken);
                    Interlocked.Increment(ref _messagesProcessed);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // Propagate cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Agent {AgentName} error processing message: {Message}",
                        _agentName, message);
                    OnProcessingError(message, ex);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Agent {AgentName} cancelled", _agentName);
        }

        _logger.LogInformation("Agent {AgentName} stopped. Processed {Count} messages",
            _agentName, MessagesProcessed);
    }

    /// <summary>
    /// Signals the agent to stop accepting new messages.
    /// </summary>
    public Task StopAsync()
    {
        _channel.Writer.Complete();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the agent by stopping it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Process a single message. Called on the agent's dedicated thread.
    /// All state mutations should happen here - no locks needed.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected abstract ValueTask ProcessMessageAsync(TMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Called when message processing throws an exception.
    /// Override to implement custom error handling.
    /// </summary>
    /// <param name="message">The message that caused the error.</param>
    /// <param name="exception">The exception that was thrown.</param>
    protected virtual void OnProcessingError(TMessage message, Exception exception)
    {
        // Default: log and continue. Override for custom behavior.
    }
}
