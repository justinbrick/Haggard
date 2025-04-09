using System.Numerics;
using Microsoft.Extensions.Logging;

namespace Haggard.Engine;

public sealed class HaggardGameEngine : IGameEngine
{
    public event IGameEngine.EngineTickEvent Tick;
    public event IGameEngine.EngineRenderEvent Render;
    public event Action? Started;
    public event Action? Starting;
    
    private readonly ILogger<HaggardGameEngine> _logger;

    public HaggardGameEngine(ILogger<HaggardGameEngine> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Haggard Game Engine");
        Starting?.Invoke(); 
        _logger.LogInformation("Haggard Game Engine Started");
        Started?.Invoke();
        while (!cancellationToken.IsCancellationRequested)
        {
            Tick.Invoke(10);
            Render.Invoke(10);
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
        }
    }


}