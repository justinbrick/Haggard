using System.ComponentModel;
using System.Runtime.InteropServices;
using Haggard.Engine.Client.Graphics.Devices;
using Silk.NET.Vulkan;

namespace Haggard.Engine.Client.Graphics.Vulkan;

public sealed class VulkanDeviceManager(VulkanRenderingSystem renderingSystem) : IDeviceManager
{
    /// <summary>
    /// Enumerates a list of physical devices, with their properties, from the Vulkan API.
    /// </summary>
    /// <returns>an enumeration with a tuple of the device handle, and the properties of the device</returns>
    public unsafe IEnumerable<(PhysicalDevice, PhysicalDeviceProperties2)> GetPhysicalDevices()
    {
        uint deviceCount;
        renderingSystem.Vulkan.EnumeratePhysicalDevices(
            renderingSystem.Instance,
            &deviceCount,
            null
        );

        var physicalDevices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* pPhysicalDevices = physicalDevices)
        {
            renderingSystem.Vulkan.EnumeratePhysicalDevices(
                renderingSystem.Instance,
                &deviceCount,
                pPhysicalDevices
            );
        }

        return physicalDevices.Select(d =>
            (d, renderingSystem.Vulkan.GetPhysicalDeviceProperties2(d))
        );
    }

    public IEnumerable<RenderingDevice> GetDevices()
    {
        return GetPhysicalDevices()
            .Select(d =>
            {
                var p = d.Item2;
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

    public bool TrySelectDevice(RenderingDevice device)
    {
        var physicalDevices = GetPhysicalDevices();
        if (device.Id is not null)
            // If the device ID is not null, just try to do an exact match.
            physicalDevices = physicalDevices.Where(d => d.Item2.Properties.DeviceID == device.Id);
        else if (device.Name is not null)
            // If the device ID is null, but we have a name, try and match based off of name.
            physicalDevices = physicalDevices.Where(d =>
            {
                string name;
                unsafe
                {
                    name =
                        Marshal.PtrToStringUTF8((IntPtr)d.Item2.Properties.DeviceName)
                        ?? string.Empty;
                }

                return name == device.Name;
            });
        else if (device.Vendor is not null)
        {
            // If all else fails, attempt to look for a vendor ID based off of the vendor string.
            if (!uint.TryParse(device.Vendor, out var vendorId))
            {
                return false;
            }
            physicalDevices = physicalDevices.Where(d => d.Item2.Properties.VendorID == vendorId);
        }

        var deviceInfo = physicalDevices.FirstOrDefault();
        if (deviceInfo.Item1.Handle == IntPtr.Zero)
        {
            return false;
        }

        throw new NotImplementedException();

        return true;
    }

    public bool TrySelectDevice(DeviceSelectionStrategy selectionStrategy)
    {
        throw new NotImplementedException();
    }
}
