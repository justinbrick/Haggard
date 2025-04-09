namespace Haggard.Engine;

public class HaggardGameEngine : IGameEngine
{
    public event Action<float>? Tick;
    public event Action<float>? Render;

    public HaggardGameEngine()
    {
        
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Tick?.Invoke(10);
            await Task.Delay(TimeSpan.FromSeconds(1),CancellationToken.None);
        }
    }


}