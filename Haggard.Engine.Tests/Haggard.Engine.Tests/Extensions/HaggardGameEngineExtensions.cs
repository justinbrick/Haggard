namespace Haggard.Engine.Tests.Extensions;

public static class HaggardGameEngineExtensions
{
    public static Task StartBackground(this HaggardGameEngine engine)
    {
        ThreadPool.QueueUserWorkItem(_ => engine.Start(CancellationToken.None));
        return Task.Delay(TimeSpan.FromMilliseconds(100));
    }
}