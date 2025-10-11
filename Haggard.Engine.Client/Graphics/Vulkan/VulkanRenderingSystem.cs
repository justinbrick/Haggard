using System.Runtime.InteropServices;
using Haggard.Engine.Client.Graphics.Devices;
using Haggard.Engine.Client.Windowing;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Haggard.Engine.Client.Graphics.Vulkan;

/// <summary>
/// A rendering system implementation using Vulkan.
/// </summary>
public sealed unsafe class VulkanRenderingSystem : IRenderingSystem, IDisposable
{
    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];
    private readonly ILogger<VulkanRenderingSystem> _logger;
    private readonly IGameEngine _gameEngine;
    private readonly VulkanRenderingSystemOptions _options = new();
    private readonly VulkanDeviceManager _vulkanDeviceManager;
    private readonly IWindowManager _windowManager;
    public readonly Vk Vulkan = Vk.GetApi();
    internal Instance Instance;
    public IDeviceManager DeviceManager => _vulkanDeviceManager;
    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _presentQueue;
    private KhrSurface _khrSurface = null!;
    private SurfaceKHR _surface;

    public VulkanRenderingSystem(
        ILogger<VulkanRenderingSystem> logger,
        IGameEngine gameEngine,
        IWindowManager windowManager
    )
    {
        _vulkanDeviceManager = new VulkanDeviceManager(this);
        _vulkanDeviceManager.OnDeviceSelected += OnDeviceSelected;
        _logger = logger;
        _gameEngine = gameEngine;
        _windowManager = windowManager;
        windowManager.CurrentWindow.Closing += OnWindowClosing;

        InitializeVulkan();
        InitializeDebugger();
        InitializeSurface();
        InitializeDevice();
    }

    private void OnWindowClosing() { }

    private sealed class Families
    {
        /// <summary>
        /// The index of a queue that is supporting graphics.
        /// </summary>
        public uint? Graphics;

        /// <summary>
        /// The index of a queue in the presentation family.
        /// </summary>
        public uint? Presentation;

        public HashSet<uint> ToSet()
        {
            var set = new HashSet<uint>();
            if (Graphics != null)
            {
                set.Add(Graphics.Value);
            }

            if (Presentation != null)
            {
                set.Add(Presentation.Value);
            }

            return set;
        }
    }

    private Families GetQueueFamilies(in PhysicalDevice physicalDevice)
    {
        var family = new Families();
        uint familySize;
        Vulkan.GetPhysicalDeviceQueueFamilyProperties2(physicalDevice, &familySize, null);
        var familyProperties = new QueueFamilyProperties2[familySize];
        for (var i = 0; i < familyProperties.Length; i++)
        {
            familyProperties[i].SType = StructureType.QueueFamilyProperties2;
        }
        fixed (QueueFamilyProperties2* pFamilyProperties = familyProperties)
            Vulkan.GetPhysicalDeviceQueueFamilyProperties2(
                physicalDevice,
                &familySize,
                pFamilyProperties
            );

        for (uint i = 0; i < familyProperties.Length; i++)
        {
            var properties = familyProperties[i];
            var flags = properties.QueueFamilyProperties.QueueFlags;
            if (flags.HasFlag(QueueFlags.GraphicsBit))
                family.Graphics = i;
            _khrSurface.GetPhysicalDeviceSurfaceSupport(
                physicalDevice,
                i,
                _surface,
                out var supported
            );
            if (supported)
            {
                family.Presentation = i;
            }
        }

        return family;
    }

    private void OnDeviceSelected(
        PhysicalDevice physicalDevice,
        PhysicalDeviceProperties2 physicalDeviceProperties
    ) { }

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

    private void InitializeVulkan()
    {
        if (_windowManager.CurrentWindow.VkSurface is null)
            throw new Exception("IWindow.VkSurface is null.");

        if (_options.EnableValidationLayers && !EnsureValidationLayers())
            throw new Exception("Validation layers not found. Are proper libraries installed?");

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

        var requiredExtensions = GetRequiredInstanceExtensions();
        var pRequiredExtensions = (byte**)SilkMarshal.StringArrayToPtr(requiredExtensions);
        createInfo.EnabledExtensionCount = (uint)requiredExtensions.Length;
        createInfo.PpEnabledExtensionNames = pRequiredExtensions;

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
        if (!_options.EnableValidationLayers)
            return;

        if (!Vulkan.TryGetInstanceExtension<ExtDebugUtils>(Instance, out var debugUtils))
            throw new Exception(
                "Could not get debug layers, but enable validation layers is enabled. Are they installed?"
            );

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

    private void InitializeSurface()
    {
        if (_windowManager.CurrentWindow.VkSurface is not { } surface)
            throw new Exception("Could not get Vulkan Surface.");

        if (!Vulkan.TryGetInstanceExtension(Instance, out _khrSurface))
        {
            throw new Exception("Failed to get KhrSurface.");
        }
        _surface = surface.Create<AllocationCallbacks>(Instance.ToHandle(), null).ToSurface();
    }

    private void InitializeDevice()
    {
        var maybeDevice = _vulkanDeviceManager.GetPhysicalDevice(
            _vulkanDeviceManager.GetDevices().First()
        );

        if (maybeDevice is not { } deviceTuple)
            throw new Exception("Could not get device!");

        var device = deviceTuple.Item1;
        if (
            GetQueueFamilies(device)
            is not { Graphics: { } graphics, Presentation: { } present } families
        )
            throw new Exception("Could not get graphics or queue family!");

        var queuePriority = 1f;
        var queueCreateInfos = families
            .ToSet()
            .Select(static i => new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = i,
                QueueCount = 1,
                PQueuePriorities = null,
            })
            .ToArray();

        for (var i = 0; i < queueCreateInfos.Length; i++)
        {
            queueCreateInfos[i].PQueuePriorities = &queuePriority;
        }

        var deviceExtensions = GetRequiredDeviceExtensions();
        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                EnabledExtensionCount = (uint)deviceExtensions.Length,
                EnabledLayerCount = 0,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions),
                PpEnabledLayerNames = null,
                PQueueCreateInfos = pQueueCreateInfos,
                QueueCreateInfoCount = (uint)queueCreateInfos.Length,
            };
            fixed (Device* pDevice = &_device)
            {
                if (Vulkan.CreateDevice(device, &createInfo, null, pDevice) != Result.Success)
                {
                    throw new Exception("Failed to create logical device!");
                }
            }
            SilkMarshal.Free((IntPtr)createInfo.PpEnabledExtensionNames);
        }

        Vulkan.GetDeviceQueue(_device, graphics, 0, out _graphicsQueue);
        Vulkan.GetDeviceQueue(_device, present, 0, out _presentQueue);
    }

    private string[] GetRequiredInstanceExtensions()
    {
        var extensions = _windowManager.CurrentWindow.VkSurface!.GetRequiredExtensions(
            out var count
        );
        var list = SilkMarshal.PtrToStringArray((IntPtr)extensions, (int)count).ToList();
        if (_options.EnableValidationLayers)
            list.Add(ExtDebugUtils.ExtensionName);

        return list.ToArray();
    }

    private string[] GetRequiredDeviceExtensions()
    {
        return [KhrSwapchain.ExtensionName];
    }

    private bool EnsureValidationLayers()
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

    public void Dispose()
    {
        Vulkan.DestroyDevice(_device, null);
        _khrSurface.DestroySurface(Instance, _surface, null);
        _debugUtils?.DestroyDebugUtilsMessenger(Instance, _debugMessenger, null);
        Vulkan.DestroyInstance(Instance, null);
        Vulkan.Dispose();
    }
}
