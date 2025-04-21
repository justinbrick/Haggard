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
        await gameEngine.StartBackground();
        Assert.NotNull(windowManager.CurrentWindow);
        Assert.False(windowManager.CurrentWindow.IsClosing);
    }
}
