using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Haggard.Engine.Client.Graphics.Devices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Haggard.Engine.Client.Graphics.Vulkan;

public sealed class VulkanDeviceManager(VulkanRenderingSystem renderingSystem)
    : IDeviceManager,
        IDisposable
{
    public struct DeviceDetails
    {
        public PhysicalDevice PhysicalDevice;
        public PhysicalDeviceProperties2 PhysicalDeviceProperties;
        public PhysicalDeviceMemoryProperties MemoryProperties;
        public PhysicalDeviceFeatures2 PhysicalDeviceFeatures;
        public IReadOnlySet<string> AvailableExtensions;
        public HashSet<string> RequiredExtensions;
    }

    public delegate void DeviceSelectedEvent(in DeviceDetails details);
    public delegate void DeviceSuitableQuery(in DeviceDetails details, ref bool isSuitable);

    public event DeviceSelectedEvent? OnDeviceSelected;
    public event DeviceSuitableQuery? OnDeviceSuitableQuery;
    public PhysicalDevice? CurrentPhysicalDevice;
    public Device? CurrentLogicalDevice;

    /// <summary>
    /// Enumerates a list of physical devices, with their properties, from the Vulkan API.
    /// </summary>
    /// <returns>an enumeration with a tuple of the device handle, and the properties of the device</returns>
    public IEnumerable<DeviceDetails> GetPhysicalDevices()
    {
        uint deviceCount = 0;
        unsafe
        {
            renderingSystem.Vulkan.EnumeratePhysicalDevices(
                renderingSystem.Instance,
                &deviceCount,
                null
            );
        }

        var physicalDevices = new PhysicalDevice[deviceCount];
        unsafe
        {
            fixed (PhysicalDevice* pPhysicalDevices = physicalDevices)
            {
                renderingSystem.Vulkan.EnumeratePhysicalDevices(
                    renderingSystem.Instance,
                    &deviceCount,
                    pPhysicalDevices
                );
            }
        }

        foreach (var device in physicalDevices)
        {
            var deviceProperties = new PhysicalDeviceProperties2();
            var deviceFeatures = new PhysicalDeviceFeatures2();
            PhysicalDeviceMemoryProperties memoryProperties;
            HashSet<string> availableExtensions;

            unsafe
            {
                // Set properties
                renderingSystem.Vulkan.GetPhysicalDeviceProperties2(device, &deviceProperties);
                renderingSystem.Vulkan.GetPhysicalDeviceMemoryProperties(device, &memoryProperties);
                renderingSystem.Vulkan.GetPhysicalDeviceFeatures2(device, &deviceFeatures);

                // Set extensions
                uint extensionCount = 0;
                renderingSystem.Vulkan.EnumerateDeviceExtensionProperties(
                    device,
                    (byte*)null,
                    ref extensionCount,
                    null
                );
                var extensionProperties = new ExtensionProperties[extensionCount];
                fixed (ExtensionProperties* pExtensionProperties = extensionProperties)
                {
                    renderingSystem.Vulkan.EnumerateDeviceExtensionProperties(
                        device,
                        (byte*)null,
                        ref extensionCount,
                        pExtensionProperties
                    );
                }
                availableExtensions = extensionProperties
                    .Select(p => SilkMarshal.PtrToString((IntPtr)p.ExtensionName))
                    .Where(p => p is not null)
                    .Cast<string>()
                    .ToHashSet();
            }
            // Query device suitability, then yield return.
            var details = new DeviceDetails
            {
                PhysicalDevice = device,
                PhysicalDeviceProperties = deviceProperties,
                MemoryProperties = memoryProperties,
                PhysicalDeviceFeatures = deviceFeatures,
                AvailableExtensions = availableExtensions,
                RequiredExtensions = [],
            };

            var isSuitable = true;
            OnDeviceSuitableQuery?.Invoke(in details, ref isSuitable);
            if (isSuitable)
                yield return details;
        }
    }

    public IEnumerable<RenderingDevice> GetDevices()
    {
        return GetPhysicalDevices()
            .Select(d =>
            {
                var p = d.PhysicalDeviceProperties;
                string deviceName;
                unsafe
                {
                    deviceName =
                        Marshal.PtrToStringUTF8((IntPtr)p.Properties.DeviceName)
                        ?? throw new NullReferenceException("Device name is null.");
                }

                return new RenderingDevice
                {
                    Id = p.Properties.DeviceID,
                    Name = deviceName,
                    Vendor = p.Properties.VendorID.ToString(),
                };
            });
    }

    public DeviceDetails? GetPhysicalDevice(RenderingDevice device)
    {
        var physicalDevices = GetPhysicalDevices();
        if (device.Id is not null)
            // If the device ID is not null, just try to do an exact match.
            physicalDevices = physicalDevices.Where(d =>
                d.PhysicalDeviceProperties.Properties.DeviceID == device.Id
            );
        else if (device.Name is not null)
            // If the device ID is null, but we have a name, try and match based off of name.
            physicalDevices = physicalDevices.Where(d =>
            {
                string name;
                unsafe
                {
                    name =
                        Marshal.PtrToStringUTF8(
                            (IntPtr)d.PhysicalDeviceProperties.Properties.DeviceName
                        ) ?? string.Empty;
                }

                return name == device.Name;
            });
        else if (device.Vendor is not null)
        {
            // If all else fails, attempt to look for a vendor ID based off of the vendor string.
            if (!uint.TryParse(device.Vendor, out var vendorId))
            {
                return null;
            }
            physicalDevices = physicalDevices.Where(d =>
                d.PhysicalDeviceProperties.Properties.VendorID == vendorId
            );
        }

        var deviceInfo = physicalDevices.FirstOrDefault();
        if (deviceInfo.PhysicalDevice.Handle == IntPtr.Zero)
        {
            return null;
        }

        return deviceInfo;
    }

    public bool TrySelectDevice(RenderingDevice device)
    {
        var selected = GetPhysicalDevice(device);
        if (selected is not { } selectedDevice)
        {
            return false;
        }

        SetPhysicalDevice(selectedDevice);
        return true;
    }

    public bool TrySelectDevice(DeviceSelectionStrategy selectionStrategy)
    {
        var devices = GetPhysicalDevices();
        if (
            selectionStrategy.HasFlag(DeviceSelectionStrategy.PreferIntegrated)
            && selectionStrategy.HasFlag(DeviceSelectionStrategy.PreferDedicated)
        )
        {
            throw new InvalidEnumArgumentException(
                "selectionStrategy contains both integrated & dedicated. Only one can be chosen."
            );
        }

        if (selectionStrategy.HasFlag(DeviceSelectionStrategy.PreferIntegrated))
        {
            devices = devices.Where(d =>
                d.PhysicalDeviceProperties.Properties.DeviceType == PhysicalDeviceType.IntegratedGpu
            );
        }

        if (selectionStrategy.HasFlag(DeviceSelectionStrategy.PreferDedicated))
        {
            devices = devices.Where(d =>
                d.PhysicalDeviceProperties.Properties.DeviceType == PhysicalDeviceType.DiscreteGpu
            );
        }

        if (selectionStrategy.HasFlag(DeviceSelectionStrategy.HighestMemory))
        {
            devices = devices.OrderBy(d =>
            {
                ulong totalMemory = 0;
                for (var i = 0; i < d.MemoryProperties.MemoryHeapCount; ++i)
                {
                    // TODO: Flawed implementation. Look at device flags to find memory that is the best.
                    var heap = d.MemoryProperties.MemoryHeaps[i];
                    if (!heap.Flags.HasFlag(MemoryHeapFlags.DeviceLocalBit))
                        continue;

                    totalMemory += heap.Size;
                }

                return totalMemory;
            });
        }

        var selected = devices.FirstOrDefault();
        if (selected.PhysicalDevice.Handle == IntPtr.Zero)
            return false;

        SetPhysicalDevice(selected);
        return true;
    }

    /// <summary>
    /// Actually sets the selected device, clearing any previous ones, if they existed.
    /// </summary>
    /// <param name="physicalDevice"></param>
    private void SetPhysicalDevice(in DeviceDetails details)
    {
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

        var deviceExtensions = details.RequiredExtensions.ToArray();
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

    public void EnsureDeviceRemoved()
    {
        if (CurrentLogicalDevice is null)
            return;

        unsafe
        {
            renderingSystem.Vulkan.DestroyDevice(CurrentLogicalDevice.Value, null);
        }

        CurrentLogicalDevice = null;
        CurrentPhysicalDevice = null;
    }

    public void Dispose()
    {
        EnsureDeviceRemoved();
    }
}
