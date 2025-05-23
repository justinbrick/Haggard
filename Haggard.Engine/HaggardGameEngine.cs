﻿using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Haggard.Engine;

public sealed class HaggardGameEngine : IGameEngine
{
    private readonly ILogger<HaggardGameEngine> _logger;

    private readonly HaggardGameEngineOptions _options = new HaggardGameEngineOptions
    {
        TickRate = 60
    };
    private CancellationToken _cancellationToken;
    public event IGameEngine.EngineTickEvent? Tick;
    public event Action? Started;
    public event Action? Starting;
    public event Action? Stopping;
    public string Name => "Haggard";

    public HaggardGameEngine(ILogger<HaggardGameEngine> logger)
    {
        _logger = logger;
    }

    public void Stop()
    {
        _logger.LogInformation("Forcing engine to stop");
        _cancellationToken = new CancellationToken(true);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _logger.LogInformation("Starting engine");
        Starting?.Invoke(); 
        _logger.LogInformation("Started engine");
        Started?.Invoke();
        var tickWait = TimeSpan.TicksPerSecond / _options.TickRate;
        var tickWatch = new Stopwatch();
        while (!_cancellationToken.IsCancellationRequested)
        {
            var elapsed = tickWatch.ElapsedTicks;
            if (elapsed < tickWait) 
                new ManualResetEvent(false).WaitOne((int)((tickWait-elapsed)/TimeSpan.TicksPerMillisecond));

            var newTick = (float)tickWatch.ElapsedTicks/TimeSpan.TicksPerMillisecond;
            tickWatch.Restart();
            Tick?.Invoke(newTick);
        }
    
        _logger.LogInformation("Stopping engine");
        Stopping?.Invoke();
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        // TODO: Research this impl. Right now, there's a timespan, but this could be a "graceful shutdown" period.
        // In which case, we need to make that configurable.
        return Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    }
}