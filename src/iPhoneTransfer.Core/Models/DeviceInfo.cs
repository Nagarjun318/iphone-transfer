namespace iPhoneTransfer.Core.Models;

/// <summary>
/// Represents a connected iPhone device.
/// WHY: Encapsulates device identification and connection state.
/// </summary>
public class DeviceInfo
{
    /// <summary>
    /// Unique Device Identifier (40-character hex string)
    /// WHY: Used to locate pairing records, distinguish multiple iPhones
    /// Example: "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
    /// </summary>
    public string UDID { get; set; } = string.Empty;

    /// <summary>
    /// User-assigned device name (e.g., "John's iPhone")
    /// WHY: Display in UI for friendly device selection
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device model (e.g., "iPhone 14 Pro", "iPhone SE (3rd generation)")
    /// WHY: Display in UI, debug device-specific issues
    /// </summary>
    public string ProductType { get; set; } = string.Empty;

    /// <summary>
    /// iOS version (e.g., "16.5", "17.2.1")
    /// WHY: Warn user if unsupported iOS version, debug protocol changes
    /// </summary>
    public string ProductVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether the device is currently locked
    /// WHY: AFC service requires unlocked device - show "Please unlock your iPhone" message
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Whether this PC is trusted ("Trust This Computer" completed)
    /// WHY: If false, show instructions to trust, can't access files until trusted
    /// </summary>
    public bool IsPaired { get; set; }

    /// <summary>
    /// Connection type (USB2, USB3, Wi-Fi)
    /// WHY: Warn user if using slow USB 2.0 for large transfers
    /// </summary>
    public ConnectionType ConnectionType { get; set; }

    /// <summary>
    /// Battery level (0-100)
    /// WHY: Warn user if battery is low (transfers can take minutes)
    /// </summary>
    public int BatteryLevel { get; set; }

    /// <summary>
    /// Whether battery is currently charging
    /// WHY: Combined with BatteryLevel, determine if safe to start long transfer
    /// </summary>
    public bool IsCharging { get; set; }

    /// <summary>
    /// Available storage on iPhone in bytes
    /// WHY: Informational - not critical for transfer (we're copying FROM iPhone)
    /// </summary>
    public long AvailableStorage { get; set; }

    /// <summary>
    /// When this device was first connected/detected
    /// WHY: Logging, debug reconnection issues
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Display string for UI
    /// Example: "John's iPhone (iPhone 14 Pro) - iOS 16.5"
    /// </summary>
    public string DisplayName => $"{DeviceName} ({ProductType}) - iOS {ProductVersion}";
}

/// <summary>
/// How the iPhone is connected to the PC.
/// WHY: Affects transfer speed - USB 3.0 is 5x faster than USB 2.0.
/// </summary>
public enum ConnectionType
{
    /// <summary>Unknown connection method</summary>
    Unknown,
    
    /// <summary>
    /// USB 2.0 (480 Mbps theoretical, ~40 MB/s real-world)
    /// WHY: Most common - many iPhone cables are USB 2.0
    /// </summary>
    USB2,
    
    /// <summary>
    /// USB 3.0+ (5 Gbps theoretical, ~200 MB/s real-world)
    /// WHY: Requires USB-C cable or Lightning-to-USB3 adapter
    /// </summary>
    USB3,
    
    /// <summary>
    /// Wi-Fi sync (much slower, not recommended for this app)
    /// WHY: Requires iTunes Wi-Fi sync enabled, highly variable speed
    /// </summary>
    WiFi
}
