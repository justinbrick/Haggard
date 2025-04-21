namespace Haggard.Engine.Tests.Extensions;

public static class HaggardGameEngineExtensions
{
    /// <summary>
    /// Starts the game engine in the background, and then waits for a small amount of time to wait for initialization.
    /// </summary>
    /// <returns>a timer to await to ensure proper initialization.</returns>
    public static async Task StartBackground(this HaggardGameEngine engine)
    {
        var tokenSource = new CancellationTokenSource();
        _ = Task.Run(() => engine.StartAsync(tokenSource.Token).GetAwaiter().GetResult(), tokenSource.Token);
        await Task.Delay(TimeSpan.FromSeconds(5), tokenSource.Token);
        await tokenSource.CancelAsync();
    }
}