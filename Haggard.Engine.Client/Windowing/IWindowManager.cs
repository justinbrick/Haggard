using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Windowing;

public interface IWindowManager
{
    /// <summary>
    /// A handler for when the game renders a frame.
    /// <param name="deltaTime">the amount of time in seconds that have passed since the last frame</param>
    /// </summary>
    public delegate void WindowRenderEvent(float deltaTime);
    /// <summary>
    /// A handler for when the window has just been created. <br/>
    /// This can be used for systems that directly depend on using Silk.NET's window, rather than engine startup.
    /// <param name="window">the instance of the window that has been created from the IWindowManager</param>
    /// </summary>
    public delegate void WindowCreatedEvent(IWindow window);
    /// <summary>
    /// Called when the game engine window undergoes it's rendering phase.
    /// </summary>
    public event WindowRenderEvent Render;
    /// <summary>
    /// Called when the window has been created.
    /// </summary>
    public event WindowCreatedEvent WindowCreated;
    public IWindow? CurrentWindow { get; }
}