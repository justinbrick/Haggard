namespace Haggard.Engine.Tests;

public class EngineTests
{
    [Fact]
    public async Task EngineTicksEvent()
    {
        var cancellationToken = new CancellationTokenSource();
        var engine = new HaggardGameEngine();
        var tick = 0;
        engine.Tick += (_) => tick++;

        await Task.WhenAll(engine.RunAsync(cancellationToken.Token), CancelTask());
        
        Assert.NotEqual(0, tick);
        return;

        async Task CancelTask()
        {
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            await cancellationToken.CancelAsync();
        }
    }
}