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
    public delegate void EngineTickEvent(float deltaTime);
    /// <summary>
    /// A handler for when the game renders a frame.
    /// <param name="deltaTime">the amount of time in seconds that have passed since the last frame</param>
    /// </summary>
    public delegate void EngineRenderEvent(float deltaTime);
    /// <summary>
    /// Called when the game engine goes through a "tick".
    /// </summary>
    public event EngineTickEvent Tick;
    /// <summary>
    /// Called when the game engine undergoes it's rendering phase.
    /// </summary>
    public event EngineRenderEvent Render;
    /// <summary>
    /// Called when the game is first starting.
    /// </summary>
    public event Action Starting;
    /// <summary>
    /// Called when the game engine has finished loading, and can be considered "started."
    /// </summary>
    public event Action Started;
    /// <summary>
    /// Called when the game engine is stopping.
    /// </summary>
    public event Action Stopping;

    public Task StartAsync(CancellationToken cancellationToken = default);
    public void Stop();
}