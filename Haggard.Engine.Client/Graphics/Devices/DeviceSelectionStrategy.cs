namespace Haggard.Engine.Client.Graphics.Devices;

/// <summary>
/// A strategy for picking a rendering device from an <see cref="IDeviceManager"/>
/// </summary>
public enum DeviceSelectionStrategy
{
    // Prefer an integrated graphics card (CPU)
    PreferIntegrated,
    // Prefer a dedicated graphics card (GPU)
    PreferDedicated,
    // Prefer a device with the highest memory.
    HighestMemory
}