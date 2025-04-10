using Microsoft.Extensions.Logging;
using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Windowing;

public sealed class HaggardWindowManager
{
    private readonly ILogger<HaggardWindowManager> _logger;
    private readonly IGameEngine _gameEngine;
    private bool _windowStopping = false;
    private bool _engineStopping = false;

    public IWindow? CurrentWindow { get; private set; }

    private void SetWindow(IWindow window)
    {
        if (CurrentWindow != null)
        {
            throw new Exception("Haggard Windowing Manager has already been started");
        }
        window.Closing += OnWindowClosing;
        CurrentWindow = window;
    }

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
        CurrentWindow = Window.Create(WindowOptions.DefaultVulkan);
        CurrentWindow.Run();
    }

    private void OnWindowClosing()
    {
        _windowStopping = true;
        if (_engineStopping) return;
        _logger.LogTrace("Window closed, stopping engine");
    }

    private void OnEngineStopping()
    {
        _engineStopping = true;
        
        if (_windowStopping) return;
        _logger.LogTrace("Closing window gracefully");
        CurrentWindow?.Close();
        CurrentWindow?.Dispose();
    }
}