using iPhoneTransfer.Core.Models;

namespace iPhoneTransfer.Core.Interfaces;

/// <summary>
/// Service for detecting and managing iPhone device connections.
/// WHY: Abstracts usbmuxd communication, enables testing with mock devices.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Get all currently connected iPhone devices.
    /// WHY: Support multiple iPhones connected simultaneously.
    /// </summary>
    /// <returns>List of connected devices (empty if none)</returns>
    Task<List<DeviceInfo>> GetConnectedDevicesAsync();

    /// <summary>
    /// Wait for an iPhone to be connected.
    /// WHY: Better UX than polling - blocks until device appears.
    /// </summary>
    /// <param name="timeout">Maximum wait time</param>
    /// <param name="cancellationToken">Allow user to cancel wait</param>
    /// <returns>First device that connects, or null if timeout</returns>
    Task<DeviceInfo?> WaitForDeviceAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific device is still connected.
    /// WHY: Detect disconnections during transfer, show "reconnecting..." message.
    /// </summary>
    /// <param name="udid">Device unique identifier</param>
    Task<bool> IsDeviceConnectedAsync(string udid);

    /// <summary>
    /// Initiate pairing ("Trust This Computer") with a device.
    /// WHY: Required before any file access, stores pairing record on Windows.
    /// </summary>
    /// <param name="udid">Device to pair with</param>
    /// <returns>True if pairing succeeded (user tapped Trust), false otherwise</returns>
    Task<bool> PairDeviceAsync(string udid);

    /// <summary>
    /// Check if this PC is already paired with the device.
    /// WHY: Skip pairing prompt if already trusted, faster startup.
    /// </summary>
    /// <param name="udid">Device to check</param>
    Task<bool> IsPairedAsync(string udid);

    /// <summary>
    /// Validate that the pairing record is still valid.
    /// WHY: Pairing becomes invalid if iPhone is reset or "Forget This Computer" is tapped.
    /// </summary>
    /// <param name="udid">Device to validate</param>
    Task<bool> ValidatePairingAsync(string udid);

    /// <summary>
    /// Event raised when a device is connected.
    /// WHY: Update UI immediately without polling.
    /// </summary>
    event EventHandler<DeviceInfo>? DeviceConnected;

    /// <summary>
    /// Event raised when a device is disconnected.
    /// WHY: Pause transfers, show "device disconnected" warning.
    /// </summary>
    event EventHandler<string>? DeviceDisconnected; // string = UDID
}
