using Haggard.Engine.Client.Graphics.Devices;

namespace Haggard.Engine.Client.Graphics.Vulkan;

public sealed class VulkanDeviceManager : IDeviceManager
{
    public IEnumerable<RenderingDevice> GetDevices()
    {
        throw new NotImplementedException();
    }

    public bool TrySelectDevice(RenderingDevice device)
    {
        throw new NotImplementedException();
    }

    public bool TrySelectDevice(DeviceSelectionStrategy selectionStrategy)
    {
        throw new NotImplementedException();
    }
}