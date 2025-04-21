using Microsoft.Extensions.Hosting;

namespace Haggard.Engine;

/// <summary>
/// A game engine is used to initialize and create the code necessary for the first startup procedures.
/// </summary>
public interface IGameEngine : IHostedService
{
    /// <summary>
    /// A handler for when the game "ticks".
    /// <param name="deltaTime">the amount of time in milliseconds that have passed since the last tick.</param>
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
    string Name { get; }
}