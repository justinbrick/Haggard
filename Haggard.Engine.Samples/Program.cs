using Haggard.Engine;
using Haggard.Engine.Client.Graphics;
using Haggard.Engine.Client.Graphics.Vulkan;
using Haggard.Engine.Client.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

var builder = Host.CreateApplicationBuilder();
builder
    .Services.AddSingleton<IGameEngine, HaggardGameEngine>()
    .AddSingleton<IWindowManager, HaggardWindowManager>()
    .AddSingleton<IRenderingSystem, VulkanRenderingSystem>();

var host = builder.Build();

host.Services.GetRequiredService<IRenderingSystem>();
await host.Services.GetRequiredService<IGameEngine>().StartAsync(CancellationToken.None);
