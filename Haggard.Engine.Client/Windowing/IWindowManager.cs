namespace Haggard.Engine.Client.Windowing;

public interface IWindowManager
{
    /// <summary>
    /// A handler for when the game renders a frame.
    /// <param name="deltaTime">the amount of time in seconds that have passed since the last frame</param>
    /// </summary>
    public delegate void WindowRenderEvent(float deltaTime);
    /// <summary>
    /// Called when the game engine window undergoes it's rendering phase.
    /// </summary>
    public event WindowRenderEvent Render;
}