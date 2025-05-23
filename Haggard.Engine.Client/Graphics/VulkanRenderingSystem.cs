﻿using System.Runtime.InteropServices;
using Haggard.Engine.Client.Windowing;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Graphics;

/// <summary>
/// A rendering system implementation using Vulkan.
/// </summary>
public sealed unsafe class VulkanRenderingSystem : IRenderingSystem
{
    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];
    private readonly ILogger<VulkanRenderingSystem> _logger;
    private readonly IGameEngine _gameEngine;
    private readonly IWindowManager _windowManager;
    private VulkanRenderingSystemOptions _options = new();
    private Vk? _vulkan;
    private Instance _instance;
    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;

    public VulkanRenderingSystem(ILogger<VulkanRenderingSystem> logger, IGameEngine gameEngine, IWindowManager windowManager)
    {
        _logger = logger;
        _gameEngine = gameEngine;
        _windowManager = windowManager;
        _windowManager.WindowCreated += OnWindowCreated;
    }

    private void OnWindowCreated(IWindow window)
    { 

        window.Closing += OnWindowClosing;
        InitializeVulkan(window);
        InitializeDebugger();
    }

    private void OnWindowClosing()
    {
        if (_windowManager.CurrentWindow is not { } window)
            return;
        
        DisposeDebugger();
    }

    private DebugUtilsMessengerCreateInfoEXT CreateDebuggerInfo()
    {
        return new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.InfoBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                          DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                          DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)OnValidationDebug
        };
    }

    private IntPtr GetRequiredExtensions(IVkSurface surface, out uint extensionCount)
    {
        var extensions = surface.GetRequiredExtensions(out extensionCount);
        var managedExtensions = SilkMarshal.PtrToStringArray((nint)extensions, (int)extensionCount);
        if (_options.EnableValidationLayers)
            managedExtensions = [..managedExtensions, ExtDebugUtils.ExtensionName];
        
        ++extensionCount;

        return SilkMarshal.StringArrayToPtr(managedExtensions);
    }

    private void InitializeVulkan(IWindow window)
    {
        if (window.VkSurface is null)
            throw new Exception("IWindow.VkSurface is null");
        
        _vulkan = Vk.GetApi();
        if (_options.EnableValidationLayers && !CanSupportValidationLayers())
            throw new Exception("Vulkan cannot support validation layers");
        
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Application Name"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi(_gameEngine.Name),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
        };

        var extensions = GetRequiredExtensions(window.VkSurface, out var extensionCount);
        createInfo.EnabledExtensionCount = extensionCount;
        createInfo.PpEnabledExtensionNames = (byte**)extensions;
        
        if (_options.EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);
            var debuggerInfo = CreateDebuggerInfo();
            createInfo.PNext = &debuggerInfo;
        }

        if (_vulkan.CreateInstance(in createInfo, null, out _instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");
        
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        SilkMarshal.Free(extensions);
        
        if (_options.EnableValidationLayers)
            SilkMarshal.Free((IntPtr)createInfo.PpEnabledLayerNames);
    }

    private void InitializeDebugger()
    {
        if (_vulkan is null || !_options.EnableValidationLayers)
            return;

        if (!_vulkan.TryGetInstanceExtension(_instance, out ExtDebugUtils debugUtils))
            return;
        
        _debugUtils = debugUtils;
        
        var debuggerInfo = CreateDebuggerInfo();
        if (_debugUtils.CreateDebugUtilsMessenger(_instance, in debuggerInfo, null, out _debugMessenger) != Result.Success)
        {
            throw new Exception("Could not create debugger from validation layers!");
        }
    }

    private void DisposeDebugger()
    {
        if (_vulkan is null || !_options.EnableValidationLayers || _debugUtils is null)
            return;
        
        _debugUtils.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
    }

    private bool CanSupportValidationLayers()
    {
        if (_vulkan is null) return false;

        uint layerCount = 0;
        _vulkan.EnumerateInstanceLayerProperties(ref layerCount, null);
        
        var layerProperties = new LayerProperties[layerCount];
        fixed (LayerProperties* pLayerProperties = layerProperties)
            _vulkan.EnumerateInstanceLayerProperties(ref layerCount, pLayerProperties);

        var layerNames = layerProperties
            .Select(layer => Marshal.PtrToStringAnsi((nint)layer.LayerName))
            .ToHashSet();

        return ValidationLayers.All(layerNames.Contains);
    }

    private uint OnValidationDebug(
        DebugUtilsMessageSeverityFlagsEXT severity, 
        DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* callbackData, 
        void* userData
    )
    {
        var logLevel = severity switch
        {
            DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt => LogLevel.Trace,
            DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => LogLevel.Warning,
            DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => LogLevel.Error,
            DebugUtilsMessageSeverityFlagsEXT.None => LogLevel.Information,
            DebugUtilsMessageSeverityFlagsEXT.InfoBitExt => LogLevel.Information,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
        var message = Marshal.PtrToStringAnsi((nint)callbackData->PMessage);
        
        _logger.Log(logLevel, "Vk ValidationLayer ({Message.Type}): {Message}", type, message);
        
        return Vk.False;
    }
}