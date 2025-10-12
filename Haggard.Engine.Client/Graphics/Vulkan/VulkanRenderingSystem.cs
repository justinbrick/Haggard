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
    private Device _logicalDevice;
    private PhysicalDevice _physicalDevice;
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
        InitializeSwapchain();
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
        var stats = _vulkanDeviceManager
            .GetPhysicalDevices()
            .Select(d => new
            {
                device = d.Item1,
                properties = d.Item2,
                supportsExtensions = EnsureDeviceExtensions(d.Item1),
                families = GetQueueFamilies(d.Item1),
            })
            .FirstOrDefault(d =>
                d.supportsExtensions && d.families is { Graphics: not null, Presentation: not null }
            );

        if (stats is null)
            throw new Exception("Could not find any suitable device!");

        var queuePriority = 1f;
        var queueCreateInfos = stats
            .families.ToSet()
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
        _physicalDevice = stats.device;
        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            var createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                EnabledLayerCount = 0,
                PpEnabledLayerNames = null,
                EnabledExtensionCount = (uint)deviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions),
                PQueueCreateInfos = pQueueCreateInfos,
                QueueCreateInfoCount = (uint)queueCreateInfos.Length,
            };
            fixed (Device* pDevice = &_logicalDevice)
            {
                if (Vulkan.CreateDevice(stats.device, &createInfo, null, pDevice) != Result.Success)
                {
                    throw new Exception("Failed to create logical device!");
                }
            }
            SilkMarshal.Free((IntPtr)createInfo.PpEnabledExtensionNames);
        }

        Vulkan.GetDeviceQueue(
            _logicalDevice,
            stats.families.Graphics!.Value,
            0,
            out _graphicsQueue
        );
        Vulkan.GetDeviceQueue(
            _logicalDevice,
            stats.families.Presentation!.Value,
            0,
            out _presentQueue
        );
    }

    private void InitializeSwapchain()
    {
        var details = GetSwapchainDetails(_physicalDevice);
        var format = details.Formats.First();
        var bounds = _windowManager.CurrentWindow.FramebufferSize;

        // TODO:
        // These need to be configured as desired, and proper frameworks set up for configuring.
        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            MinImageCount = details.SurfaceCapabilities.MinImageCount + 1,
            ImageFormat = format.Format,
            ImageColorSpace = format.ColorSpace,
            ImageExtent = new Extent2D((uint)bounds.X, (uint)bounds.Y),
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            QueueFamilyIndexCount = 0,
            PQueueFamilyIndices = null,
        };

        // TODO: if present & graphics are separate, not exclusive image sharing mode.
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

    private static string[] GetRequiredDeviceExtensions()
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

    private bool EnsureDeviceExtensions(in PhysicalDevice physicalDevice)
    {
        var required = GetRequiredDeviceExtensions().ToHashSet();

        uint count = 0;
        Vulkan.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, ref count, null);
        var extensionProperties = new ExtensionProperties[count];
        fixed (ExtensionProperties* pExtensionProperties = extensionProperties)
            Vulkan.EnumerateDeviceExtensionProperties(
                physicalDevice,
                (byte*)null,
                ref count,
                pExtensionProperties
            );

        foreach (var property in extensionProperties)
        {
            var name = SilkMarshal.PtrToString((IntPtr)property.ExtensionName);
            if (name is null)
            {
                _logger.LogWarning("Found null device property, is this normal?");
                continue;
            }
            required.Remove(name);
        }

        // If there's none, all requirements met.
        return required.Count == 0;
    }

    private sealed class SwapchainDetails
    {
        public SurfaceCapabilitiesKHR SurfaceCapabilities;
        public required SurfaceFormatKHR[] Formats;
        public required PresentModeKHR[] PresentModes;
    }

    private SwapchainDetails GetSwapchainDetails(in PhysicalDevice physicalDevice)
    {
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(
            physicalDevice,
            _surface,
            out var surfaceCapabilities
        );

        // Formats
        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, _surface, &formatCount, null);
        var formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* pFormats = formats)
        {
            _khrSurface.GetPhysicalDeviceSurfaceFormats(
                physicalDevice,
                _surface,
                &formatCount,
                pFormats
            );
        }

        // Present Modes
        uint presentCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(
            physicalDevice,
            _surface,
            &presentCount,
            null
        );
        var presentModes = new PresentModeKHR[presentCount];
        fixed (PresentModeKHR* pPresentModes = presentModes)
        {
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(
                physicalDevice,
                _surface,
                &presentCount,
                pPresentModes
            );
        }

        return new SwapchainDetails
        {
            SurfaceCapabilities = surfaceCapabilities,
            Formats = formats,
            PresentModes = presentModes,
        };
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
        Vulkan.DestroyDevice(_logicalDevice, null);
        _khrSurface.DestroySurface(Instance, _surface, null);
        _debugUtils?.DestroyDebugUtilsMessenger(Instance, _debugMessenger, null);
        Vulkan.DestroyInstance(Instance, null);
        Vulkan.Dispose();
    }
}
