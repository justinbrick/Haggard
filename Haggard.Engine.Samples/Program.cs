using Haggard.Engine;
using Haggard.Engine.Client.Graphics.Vulkan;
using Haggard.Engine.Client.Windowing;
using Microsoft.Extensions.Logging.Abstractions;

var gameEngine = new HaggardGameEngine(NullLogger<HaggardGameEngine>.Instance);
var windowing = new HaggardWindowManager(NullLogger<HaggardWindowManager>.Instance, gameEngine);
var rendering = new VulkanRenderingSystem(
    NullLogger<VulkanRenderingSystem>.Instance,
    gameEngine,
    windowing
);

await gameEngine.StartAsync(CancellationToken.None);
