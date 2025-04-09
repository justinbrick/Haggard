namespace Haggard.Engine;

/// <summary>
/// A game engine is used to initialize and create the code necessary for the first startup procedures.
/// </summary>
public interface IGameEngine
{
    public event Action<float> Tick;
    public event Action<float> Render;
}