using Haggard.Engine.Client.Windowing;
using Haggard.Engine.Tests.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Haggard.Engine.Tests;

public class ClientTests
{
    [Fact]
    public async Task WindowGetsCreated()
    {
        var gameEngine = Utils.CreateBasicEngine();
        var windowManager = new HaggardWindowManager(NullLogger<HaggardWindowManager>.Instance, gameEngine);
        Assert.Null(windowManager.CurrentWindow);
        await gameEngine.TemporaryRun(TimeSpan.FromSeconds(0.25));
        Assert.NotNull(windowManager.CurrentWindow);
    }
}
