namespace Haggard.Engine.Tests.Extensions;

public static class HaggardGameEngineExtensions
{
    public static async Task TemporaryRun(this HaggardGameEngine engine, TimeSpan? timeSpan = null)
    {
        timeSpan ??= TimeSpan.FromSeconds(1); 
        
        var cancellationToken = new CancellationTokenSource();
        await Task.WhenAll(engine.StartAsync(cancellationToken.Token), CancelTask());
        return;

        async Task CancelTask()
        {
            timeSpan ??= TimeSpan.FromSeconds(1);
            await Task.Delay(timeSpan.Value, CancellationToken.None);
            await cancellationToken.CancelAsync();
        }    
    }
}