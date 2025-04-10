using System.Numerics;
using Microsoft.Extensions.Logging;

namespace Haggard.Engine;

public sealed class HaggardGameEngine : IGameEngine
{
    private readonly ILogger<HaggardGameEngine> _logger;
    private CancellationToken _cancellationToken;
    public event IGameEngine.EngineTickEvent? Tick;
    public event Action? Started;
    public event Action? Starting;
    public event Action? Stopping;

    public HaggardGameEngine(ILogger<HaggardGameEngine> logger)
    {
        _logger = logger;
    }

    public void Start(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _logger.LogInformation("Starting engine");
        Starting?.Invoke(); 
        _logger.LogInformation("Started engine");
        Started?.Invoke();
        while (!_cancellationToken.IsCancellationRequested)
        {
            Tick?.Invoke(10);
        }
        
        _logger.LogInformation("Stopping engine");
        Stopping?.Invoke();
    }

    public void Stop()
    {
        _logger.LogInformation("Forcing engine to stop");
        _cancellationToken = new CancellationToken(true);
    }
}