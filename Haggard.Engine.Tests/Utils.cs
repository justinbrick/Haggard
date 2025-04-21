using Microsoft.Extensions.Logging.Abstractions;

namespace Haggard.Engine.Tests;

public static class Utils
{
    public static HaggardGameEngine CreateBasicEngine()
    {
        return new HaggardGameEngine(NullLogger<HaggardGameEngine>.Instance);
    }
}