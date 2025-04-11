using System.Runtime.InteropServices;
using Haggard.Engine.Client.Windowing;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Graphics;

/// <summary>
/// A rendering system implementation using Vulkan.
/// </summary>
public sealed unsafe class VulkanRenderingSystem : IRenderingSystem
{
    private readonly IGameEngine _gameEngine;
    private readonly IWindowManager _windowManager;
    private Vk? _vulkan;
    private Instance _instance;

    public VulkanRenderingSystem(IGameEngine gameEngine, IWindowManager windowManager)
    {
        _gameEngine = gameEngine;
        _windowManager = windowManager;
        _windowManager.WindowCreated += OnWindowCreated;
    }

    private void OnWindowCreated(IWindow window)
    { 
        if (window.VkSurface is null)
           throw new Exception("IWindow.VkSurface is null");
        
        _vulkan = Vk.GetApi();
        
        var appInfo = new ApplicationInfo()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Application Name"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi(_gameEngine.Name),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        var createInfo = new InstanceCreateInfo()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
        };

        var extensions = window.VkSurface.GetRequiredExtensions(out var extensionCount);
        createInfo.EnabledExtensionCount = extensionCount;
        createInfo.PpEnabledExtensionNames = extensions;
        createInfo.EnabledLayerCount = 0;

        if (_vulkan.CreateInstance(in createInfo, null, out _instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");
        
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
    }
    
}