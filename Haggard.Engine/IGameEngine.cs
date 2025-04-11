namespace Haggard.Engine;

/// <summary>
/// A game engine is used to initialize and create the code necessary for the first startup procedures.
/// </summary>
public interface IGameEngine
{
    /// <summary>
    /// A handler for when the game "ticks".
    /// <param name="deltaTime">the amount of time in seconds that have passed since the last tick.</param>
    /// </summary>
    delegate void EngineTickEvent(float deltaTime);
    /// <summary>
    /// Called when the game engine goes through a "tick".
    /// </summary>
    event EngineTickEvent Tick;
    /// <summary>
    /// Called when the game is first starting.
    /// </summary>
    event Action Starting;
    /// <summary>
    /// Called when the game engine has finished loading, and can be considered "started."
    /// </summary>
    event Action Started;
    /// <summary>
    /// Called when the game engine is stopping.
    /// </summary>
    event Action Stopping;
    /// <summary>
    /// Starts the game in an asynchronous manner.
    /// </summary>
    /// <param name="cancellationToken">a cancellation token to use for a more graceful shutdown of the engine</param>
    /// <returns></returns>
    void Start(CancellationToken cancellationToken = default);
    /// <summary>
    /// Signals to the engine to stop running.
    /// This will not immediately stop, as the engine will first try to gracefully signal a stopping event before
    /// closing. This is done after it completes the latest engine tick & render.
    /// </summary>
    void Stop();
    string Name { get; }
}