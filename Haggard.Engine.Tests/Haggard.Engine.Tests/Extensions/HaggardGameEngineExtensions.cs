namespace Haggard.Engine.Tests.Extensions;

public static class HaggardGameEngineExtensions
{
    /// <summary>
    /// Starts the game engine in the background, and then waits for a small amount of time to wait for initialization.
    /// </summary>
    /// <returns>a timer to await to ensure proper initialization.</returns>
    public static Task StartBackground(this HaggardGameEngine engine)
    {
        ThreadPool.QueueUserWorkItem(_ => engine.Start(CancellationToken.None));
        return Task.Delay(TimeSpan.FromMilliseconds(10000));
    }
}