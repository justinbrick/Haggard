namespace Haggard.Engine.Client.Graphics.Devices;

/// <summary>
/// A strategy for picking a rendering device from an <see cref="IDeviceManager"/>
/// </summary>
[Flags]
public enum DeviceSelectionStrategy
{
    PreferIntegrated = 1,

    // Prefer a dedicated graphics card (GPU)
    PreferDedicated = 1 << 2,

    // Prefer a device with the highest memory.
    HighestMemory = 1 << 3,
}
