using Haggard.Engine.Client.Graphics.Devices;

namespace Haggard.Engine.Client.Graphics;

/// <summary>
/// A system designed for rendering graphics.
/// </summary>
public interface IRenderingSystem
{
    IDeviceManager DeviceManager { get; }
}