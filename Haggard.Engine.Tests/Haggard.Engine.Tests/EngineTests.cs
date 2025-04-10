using Haggard.Engine.Tests.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Haggard.Engine.Tests;

public class EngineTests
{
    [Fact]
    public async Task EngineTicksEvent()
    {
        var engine = Utils.CreateBasicEngine();
        var tick = 0;
        engine.Tick += (_) => tick++;
        await engine.StartBackground();
        Assert.NotEqual(0, tick);
    }
}