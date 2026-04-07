namespace MechanicalSympathy.Core.Infrastructure.Agents;

/// <summary>
/// Interface for Single Writer Principle agents.
/// </summary>
/// <remarks>
/// <para>
/// Agents implement the Single Writer Principle: all writes to agent state
/// occur on a dedicated thread, eliminating the need for locks and preventing
/// race conditions.
/// </para>
/// <para>
/// Multiple threads can safely send messages to the agent, but only the agent's
/// internal processing thread modifies state.
/// </para>
/// </remarks>
/// <typeparam name="TMessage">The type of messages this agent processes.</typeparam>
public interface IAgent<TMessage> : IAsyncDisposable
{
    /// <summary>
    /// Enqueue a message for processing by the agent.
    /// Thread-safe for multiple producers.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the message is enqueued.</returns>
    ValueTask SendAsync(TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the agent's processing loop.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the agent.</param>
    /// <returns>A task that completes when the agent stops.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signal the agent to stop accepting new messages and complete processing.
    /// </summary>
    /// <returns>A task that completes when the stop signal is sent.</returns>
    Task StopAsync();

    /// <summary>
    /// Gets the number of messages currently pending in the queue.
    /// </summary>
    int PendingCount { get; }
}
