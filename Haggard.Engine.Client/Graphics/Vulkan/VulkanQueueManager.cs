namespace Haggard.Engine.Client.Graphics.Vulkan;

public sealed class VulkanQueueManager
{
    private readonly VulkanRenderingSystem _rendering;
    private readonly VulkanDeviceManager _deviceManager;

    public VulkanQueueManager(VulkanRenderingSystem rendering, VulkanDeviceManager deviceManager)
    {
        _rendering = rendering;
        _deviceManager = deviceManager;

        _deviceManager.OnDeviceSuitableQuery += OnDeviceSuitable;
    }

    private void OnDeviceSuitable(
        in VulkanDeviceManager.DeviceDetails details,
        ref bool suitable
    ) { }
}
