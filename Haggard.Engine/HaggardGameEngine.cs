using System.Numerics;
using Microsoft.Extensions.Logging;

namespace Haggard.Engine;

public sealed class HaggardGameEngine : IGameEngine
{
    private readonly ILogger<HaggardGameEngine> _logger;
    private bool _shouldStop = false;
    public event IGameEngine.EngineTickEvent? Tick;
    public event IGameEngine.EngineRenderEvent? Render;
    public event Action? Started;
    public event Action? Starting;
    public event Action? Stopping;
    

    public HaggardGameEngine(ILogger<HaggardGameEngine> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Haggard Game Engine");
        Starting?.Invoke(); 
        _logger.LogInformation("Haggard Game Engine Started");
        Started?.Invoke();
        while (!cancellationToken.IsCancellationRequested && !_shouldStop)
        {
            Tick?.Invoke(10);
            Render?.Invoke(10);
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
        }
        
        Stopping?.Invoke();
    }

    public void Stop()
    {
       _shouldStop = true; 
    }
}