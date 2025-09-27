namespace Haggard.Engine.Client.Graphics.Devices;

/// <summary>
/// Defines a manager which can list and retrieve certain devices.
/// </summary>
public interface IDeviceManager
{
    /// <summary>
    /// Enumerates all installed devices that the graphics system could find.
    /// </summary>
    /// <returns>an enumeration of all available graphics devices.</returns>
    public IEnumerable<RenderingDevice> GetDevices();
    
    /// <summary>
    /// Attempts to select a device off of a pre-defined device.
    /// </summary>
    /// <param name="device">a representation of the device to select.</param>
    /// <returns>whether the manager was successful at selecting the listed device.</returns>
    public bool TrySelectDevice(RenderingDevice device);
    /// <summary>
    /// Attempts to select a device off of a selection strategy.
    /// </summary>
    /// <param name="selectionStrategy"></param>
    /// <returns></returns>
    public bool TrySelectDevice(DeviceSelectionStrategy selectionStrategy);
}