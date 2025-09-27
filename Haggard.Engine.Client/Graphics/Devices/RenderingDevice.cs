namespace Haggard.Engine.Client.Graphics.Devices;

/// <summary>
/// Specifies a generic device that can be either selected or enumerated from an <see cref="IDeviceManager"/>
/// </summary>
public struct RenderingDevice
{
    /// <summary>
    /// The ID of the device, if it has one.
    /// </summary>
    public long? Id;
    /// <summary>
    /// The name of the device.
    /// </summary>
    public string? Name;
    /// <summary>
    /// The vendor of the device.
    /// </summary>
    public string? Vendor;
}
