using Microsoft.Extensions.Logging;
using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Windowing;

public sealed class HaggardWindowManager : IWindowManager
{
    private readonly ILogger<HaggardWindowManager> _logger;
    private readonly IGameEngine _gameEngine;
    public event IWindowManager.WindowRenderEvent? Render;
    public IWindow? CurrentWindow { get; private set; }

    public HaggardWindowManager(ILogger<HaggardWindowManager> logger, IGameEngine engine)
    {
        _logger = logger;
        _gameEngine = engine;
        engine.Starting += OnEngineStarting;
        engine.Stopping += OnEngineStopping;
    }
    
    private void OnEngineStarting()
    {
        _logger.LogTrace("Initializing window during start");
        CurrentWindow = Window.Create(WindowOptions.DefaultVulkan with
        {
            IsEventDriven = true
        });
        CurrentWindow.Closing += OnWindowClosing;
        CurrentWindow.Render += OnWindowRender;
        CurrentWindow.Initialize();
        CurrentWindow.ContinueEvents();
    }

    private void OnWindowClosing()
    {
        _logger.LogTrace("Window closed, stopping engine");
        _gameEngine.Stopping -= OnEngineStopping;  
        _gameEngine.Stop();
    }

    private void OnEngineStopping()
    {
        _logger.LogTrace("Closing window gracefully");
        if (CurrentWindow is null) return;
        CurrentWindow.Closing -= OnWindowClosing;
        CurrentWindow.Close();
        CurrentWindow.Dispose();
        CurrentWindow = null;
    }

    private void OnWindowRender(double deltaTime)
    { 
        Render?.Invoke((float)deltaTime);
    }
}