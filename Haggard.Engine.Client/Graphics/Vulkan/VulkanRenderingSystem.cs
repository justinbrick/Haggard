using System.Runtime.InteropServices;
using Haggard.Engine.Client.Graphics.Devices;
using Haggard.Engine.Client.Windowing;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Graphics.Vulkan;

/// <summary>
/// A rendering system implementation using Vulkan.
/// </summary>
public sealed unsafe class VulkanRenderingSystem : IRenderingSystem
{
    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];
    private readonly ILogger<VulkanRenderingSystem> _logger;
    private readonly IGameEngine _gameEngine;
    private readonly VulkanRenderingSystemOptions _options = new();
    private readonly VulkanDeviceManager _vulkanDeviceManager;
    public readonly Vk Vulkan = Vk.GetApi();
    internal Instance Instance;
    public IDeviceManager DeviceManager => _vulkanDeviceManager;
    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;

    public VulkanRenderingSystem(
        ILogger<VulkanRenderingSystem> logger,
        IGameEngine gameEngine,
        IWindowManager windowManager
    )
    {
        _vulkanDeviceManager = new VulkanDeviceManager(this);
        _logger = logger;
        _gameEngine = gameEngine;
        var window = windowManager.CurrentWindow;
        window.Closing += OnWindowClosing;

        InitializeVulkan(window);
        if (_options.EnableValidationLayers)
        {
            InitializeDebugger();
        }
    }

    private void OnWindowClosing()
    {
        DisposeDebugger();
    }

    private DebugUtilsMessengerCreateInfoEXT CreateDebuggerInfo()
    {
        return new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity =
                DebugUtilsMessageSeverityFlagsEXT.InfoBitExt
                | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
                | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType =
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
                | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)OnValidationDebug,
        };
    }

    private string[] GetRequiredExtensions(IVkSurface surface)
    {
        var extensions = surface.GetRequiredExtensions(out var extensionCount);
        var managedExtensions = SilkMarshal.PtrToStringArray((nint)extensions, (int)extensionCount);
        if (_options.EnableValidationLayers)
        {
            managedExtensions = [.. managedExtensions, ExtDebugUtils.ExtensionName];
        }

        return managedExtensions;
    }

    private void InitializeVulkan(IWindow window)
    {
        if (window.VkSurface is null)
            throw new Exception("IWindow.VkSurface is null");

        if (_options.EnableValidationLayers && !CanSupportValidationLayers())
            throw new Exception("Validation layers are not installed, or unsupported.")
            {
                HelpLink = "https://vulkan-tutorial.com/Drawing_a_triangle/Setup/Validation_layers",
            };

        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Application Name"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi(_gameEngine.Name),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12,
        };

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
        };

        var extensions = GetRequiredExtensions(window.VkSurface);
        createInfo.EnabledExtensionCount = (uint)extensions.Length;
        createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions);

        if (_options.EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);
            var debuggerInfo = CreateDebuggerInfo();
            createInfo.PNext = &debuggerInfo;
        }

        if (Vulkan.CreateInstance(in createInfo, null, out Instance) != Result.Success)
            throw new Exception("Failed to create Vulkan instance");

        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        SilkMarshal.Free((IntPtr)createInfo.PpEnabledExtensionNames);

        if (_options.EnableValidationLayers)
            SilkMarshal.Free((IntPtr)createInfo.PpEnabledLayerNames);
    }

    private void InitializeDebugger()
    {
        if (!Vulkan.TryGetInstanceExtension(Instance, out ExtDebugUtils debugUtils))
            return;

        _debugUtils = debugUtils;

        var debuggerInfo = CreateDebuggerInfo();
        if (
            _debugUtils.CreateDebugUtilsMessenger(
                Instance,
                in debuggerInfo,
                null,
                out _debugMessenger
            ) != Result.Success
        )
        {
            throw new Exception("Could not create debugger from validation layers!");
        }
    }

    private void DisposeDebugger()
    {
        if (!_options.EnableValidationLayers || _debugUtils is null)
            return;

        _debugUtils.DestroyDebugUtilsMessenger(Instance, _debugMessenger, null);
    }

    private bool CanSupportValidationLayers()
    {
        uint layerCount = 0;
        Vulkan.EnumerateInstanceLayerProperties(ref layerCount, null);

        var layerProperties = new LayerProperties[layerCount];
        fixed (LayerProperties* pLayerProperties = layerProperties)
            Vulkan.EnumerateInstanceLayerProperties(ref layerCount, pLayerProperties);

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
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
        var message = Marshal.PtrToStringAnsi((nint)callbackData->PMessage);

        _logger.Log(logLevel, "Vk ValidationLayer ({Message.Type}): {Message}", type, message);

        return Vk.False;
    }
}
