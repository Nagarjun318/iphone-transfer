using iMobileDevice;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Plist;
using iPhoneTransfer.Core.Exceptions;
using iPhoneTransfer.Core.Interfaces;
using iPhoneTransfer.Core.Models;

namespace iPhoneTransfer.Services;

/// <summary>
/// Manages iPhone device detection, connection, and pairing using libimobiledevice.
/// WHY: Encapsulates all usbmuxd communication and Lockdown protocol complexity.
/// </summary>
public class DeviceManager : IDeviceService, IDisposable
{
    private readonly Dictionary<string, LockdownClientHandle> _activeLockdownClients = new();
    private System.Timers.Timer? _deviceWatcher;
    private readonly HashSet<string> _knownDevices = new();

    public event EventHandler<DeviceInfo>? DeviceConnected;
    public event EventHandler<string>? DeviceDisconnected;

    /// <summary>
    /// Initialize the DeviceManager and load native libraries.
    /// WHY: Must load libimobiledevice DLLs before any device operations.
    /// </summary>
    public DeviceManager()
    {
        // WHY: NativeLibraries is a static class in imobiledevice-net.
        // RegisterLibraries() locates and loads Apple's DLLs (usbmuxd, libimobiledevice, etc.)
        // from the NuGet package's runtimes folder. Must be called once before any API use.
        try
        {
            NativeLibraries.RegisterLibraries();
        }
        catch (Exception ex)
        {
            throw new iPhoneException(
                "Failed to load Apple Mobile Device Support libraries. Please install iTunes or Apple Devices app.",
                ex,
                iPhoneErrorType.UsbmuxdNotFound
            );
        }

        StartDeviceWatcher();
    }

    /// <summary>
    /// Start background thread to watch for device connections/disconnections.
    /// WHY: Detect hotplug events without polling, raise events for UI updates.
    /// </summary>
    private void StartDeviceWatcher()
    {
        // WHY: Poll every 2 seconds (balance between responsiveness and CPU usage)
        _deviceWatcher = new System.Timers.Timer(2000);
        _deviceWatcher.Elapsed += async (s, e) => await CheckForDeviceChangesAsync();
        _deviceWatcher.AutoReset = true;
        _deviceWatcher.Start();
    }

    /// <summary>
    /// Check if the device list has changed (new devices or disconnections).
    /// WHY: Trigger events for UI to update "Connect iPhone" vs "iPhone detected".
    /// </summary>
    private async Task CheckForDeviceChangesAsync()
    {
        try
        {
            var currentDevices = await GetConnectedDevicesAsync();
            var currentUDIDs = currentDevices.Select(d => d.UDID).ToHashSet();

            // Detect new devices
            foreach (var device in currentDevices)
            {
                if (!_knownDevices.Contains(device.UDID))
                {
                    _knownDevices.Add(device.UDID);
                    DeviceConnected?.Invoke(this, device);
                }
            }

            // Detect disconnected devices
            var disconnectedUDIDs = _knownDevices.Except(currentUDIDs).ToList();
            foreach (var udid in disconnectedUDIDs)
            {
                _knownDevices.Remove(udid);
                DeviceDisconnected?.Invoke(this, udid);
                
                // WHY: Clean up Lockdown client for disconnected device (prevent memory leak)
                if (_activeLockdownClients.TryGetValue(udid, out var client))
                {
                    client.Dispose();
                    _activeLockdownClients.Remove(udid);
                }
            }
        }
        catch
        {
            // WHY: Don't crash background thread on transient errors (USB glitches, etc.)
            // Silently ignore - will retry in 2 seconds
        }
    }

    /// <summary>
    /// Get all currently connected iPhone devices.
    /// IMPLEMENTATION: Uses idevice_get_device_list from libimobiledevice.
    /// </summary>
    public async Task<List<DeviceInfo>> GetConnectedDevicesAsync()
    {
        return await Task.Run(() =>
        {
            var devices = new List<DeviceInfo>();

            // WHY: Get list of UDIDs from usbmuxd
            // This returns ALL connected iOS devices (iPhone, iPad, iPod)
            ReadOnlyCollection<string> udids;
            int count;
            var ideviceError = LibiMobileDevice.Instance.iDevice.idevice_get_device_list(out udids, out count);

            if (ideviceError != iDeviceError.Success || count == 0)
            {
                return devices; // No devices connected
            }

            // WHY: For each UDID, open Lockdown connection to get device details
            foreach (var udid in udids)
            {
                try
                {
                    var deviceInfo = GetDeviceInfo(udid);
                    if (deviceInfo != null)
                    {
                        devices.Add(deviceInfo);
                    }
                }
                catch
                {
                    // WHY: One bad device shouldn't prevent seeing other devices
                    // Skip this device, continue to next
                    continue;
                }
            }

            return devices;
        });
    }

    /// <summary>
    /// Get detailed information about a specific device.
    /// WHY: Lockdown protocol provides device name, model, iOS version, battery, etc.
    /// </summary>
    private DeviceInfo? GetDeviceInfo(string udid)
    {
        iDeviceHandle deviceHandle = null;
        LockdownClientHandle lockdownHandle = null;

        try
        {
            // STEP 1: Open connection to device via usbmuxd
            // WHY: Creates TCP tunnel over USB to this specific device
            var ideviceError = LibiMobileDevice.Instance.iDevice.idevice_new(out deviceHandle, udid);
            if (ideviceError != iDeviceError.Success)
            {
                return null;
            }

            // STEP 2: Start Lockdown client (device security/pairing manager)
            // WHY: All device info and service access goes through Lockdown
            var lockdownError = LibiMobileDevice.Instance.Lockdown.lockdownd_client_new_with_handshake(
                deviceHandle, 
                out lockdownHandle, 
                "iPhoneTransfer"  // WHY: Client name shown in iPhone's "Computers" list
            );

            if (lockdownError != LockdownError.Success)
            {
                return null;
            }

            // STEP 3: Query device properties
            // WHY: Get human-readable info for UI display
            var deviceInfo = new DeviceInfo
            {
                UDID = udid,
                DeviceName = GetLockdownValue(lockdownHandle, "DeviceName") ?? "Unknown iPhone",
                ProductType = GetLockdownValue(lockdownHandle, "ProductType") ?? "Unknown",
                ProductVersion = GetLockdownValue(lockdownHandle, "ProductVersion") ?? "Unknown",
                
                // WHY: PasswordProtected=true means locked, false means unlocked
                IsLocked = GetLockdownValue(lockdownHandle, "PasswordProtected") == "true",
                
                // WHY: Check if pairing record exists in C:\ProgramData\Apple\Lockdown\{udid}.plist
                IsPaired = CheckPairingStatus(lockdownHandle),
                
                // WHY: Parse connection type from DeviceClass and ConnectionType properties
                ConnectionType = DetermineConnectionType(lockdownHandle),
                
                // WHY: BatteryCurrentCapacity is 0-100
                BatteryLevel = int.Parse(GetLockdownValue(lockdownHandle, "BatteryCurrentCapacity") ?? "0"),
                
                // WHY: BatteryIsCharging is boolean
                IsCharging = GetLockdownValue(lockdownHandle, "BatteryIsCharging") == "true"
            };

            // WHY: Cache Lockdown client for future operations (avoid reconnecting)
            _activeLockdownClients[udid] = lockdownHandle;
            lockdownHandle = null; // Prevent disposal in finally block

            return deviceInfo;
        }
        finally
        {
            // WHY: Always clean up handles to prevent resource leaks
            lockdownHandle?.Dispose();
            deviceHandle?.Dispose();
        }
    }

    /// <summary>
    /// Read a property value from Lockdown service.
    /// WHY: Lockdown stores all device info as key-value pairs (plist format).
    /// </summary>
    private string? GetLockdownValue(LockdownClientHandle lockdownHandle, string key, string? domain = null)
    {
        try
        {
            PlistHandle plistHandle;
            
            // WHY: domain = null gets general properties, specific domains (com.apple.disk_usage) for specialized data
            var error = LibiMobileDevice.Instance.Lockdown.lockdownd_get_value(
                lockdownHandle, 
                domain, 
                key, 
                out plistHandle
            );

            if (error != LockdownError.Success || plistHandle.IsInvalid)
            {
                return null;
            }

            using (plistHandle)
            {
                // WHY: Convert plist to string (handles strings, numbers, booleans)
                return LibiMobileDevice.Instance.Plist.PlistNodeToString(plistHandle);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if this PC has a valid pairing record with the device.
    /// WHY: Determines if "Trust This Computer" is needed.
    /// </summary>
    private bool CheckPairingStatus(LockdownClientHandle lockdownHandle)
    {
        try
        {
            PlistHandle pairRecordHandle;
            
            // WHY: Lockdown returns cached pairing record if it exists
            var error = LibiMobileDevice.Instance.Lockdown.lockdownd_get_pair_record(
                lockdownHandle,
                out pairRecordHandle
            );

            if (error == LockdownError.Success && !pairRecordHandle.IsInvalid)
            {
                pairRecordHandle.Dispose();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determine if device is connected via USB 2.0, USB 3.0, or Wi-Fi.
    /// WHY: Show connection speed warning if USB 2.0 (slow transfers).
    /// </summary>
    private ConnectionType DetermineConnectionType(LockdownClientHandle lockdownHandle)
    {
        try
        {
            // WHY: ConnectionType property: "USB", "WiFi", or specific USB version
            var connType = GetLockdownValue(lockdownHandle, "ConnectionType");
            
            if (connType?.Contains("WiFi") == true || connType?.Contains("Network") == true)
            {
                return ConnectionType.WiFi;
            }

            // WHY: USB 3.0 reports "Wired" or "SuperSpeed", USB 2.0 reports "USB" or "HighSpeed"
            var usbProductId = GetLockdownValue(lockdownHandle, "USBProductID");
            if (usbProductId != null)
            {
                // WHY: Apple USB Product IDs: 0x12a8 = USB 2.0, 0x12ab = USB 3.0
                // This is heuristic - not 100% reliable but good enough
                return connType?.Contains("SuperSpeed") == true ? ConnectionType.USB3 : ConnectionType.USB2;
            }

            return ConnectionType.USB2; // Default assumption
        }
        catch
        {
            return ConnectionType.Unknown;
        }
    }

    public async Task<DeviceInfo?> WaitForDeviceAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;

        while (DateTime.Now - startTime < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                return null;

            var devices = await GetConnectedDevicesAsync();
            if (devices.Count > 0)
            {
                return devices[0]; // Return first device
            }

            // WHY: Check every 500ms (responsive but not CPU-intensive)
            await Task.Delay(500, cancellationToken);
        }

        return null; // Timeout
    }

    public async Task<bool> IsDeviceConnectedAsync(string udid)
    {
        var devices = await GetConnectedDevicesAsync();
        return devices.Any(d => d.UDID == udid);
    }

    /// <summary>
    /// Initiate pairing with the device.
    /// CRITICAL: User MUST unlock iPhone and tap "Trust" for this to succeed.
    /// </summary>
    public async Task<bool> PairDeviceAsync(string udid)
    {
        return await Task.Run(() =>
        {
            iDeviceHandle deviceHandle = null;
            LockdownClientHandle lockdownHandle = null;

            try
            {
                // STEP 1: Connect to device
                var ideviceError = LibiMobileDevice.Instance.iDevice.idevice_new(out deviceHandle, udid);
                if (ideviceError != iDeviceError.Success)
                {
                    throw new iPhoneException("Failed to connect to device", iPhoneErrorType.DeviceNotFound);
                }

                // STEP 2: Create Lockdown client WITHOUT handshake (we're not paired yet)
                // WHY: lockdownd_client_new (not lockdownd_client_new_with_handshake) for unpaired device
                var lockdownError = LibiMobileDevice.Instance.Lockdown.lockdownd_client_new(
                    deviceHandle,
                    out lockdownHandle,
                    "iPhoneTransfer"
                );

                if (lockdownError != LockdownError.Success)
                {
                    throw new iPhoneException("Failed to create Lockdown client", iPhoneErrorType.ServiceUnavailable);
                }

                // STEP 3: Request pairing
                // WHY: This triggers "Trust This Computer?" dialog on iPhone
                // User has up to 30 seconds to respond
                PlistHandle pairingOptions = null; // Use default pairing options
                lockdownError = LibiMobileDevice.Instance.Lockdown.lockdownd_pair(lockdownHandle, pairingOptions);

                if (lockdownError == LockdownError.Success)
                {
                    // WHY: Pairing succeeded - record is now saved in C:\ProgramData\Apple\Lockdown\
                    return true;
                }
                else if (lockdownError == LockdownError.UserDeniedPairing)
                {
                    // WHY: User tapped "Don't Trust"
                    throw new iPhoneException("User denied pairing request", iPhoneErrorType.PairingFailed);
                }
                else if (lockdownError == LockdownError.PasswordProtected)
                {
                    // WHY: iPhone is still locked, user needs to unlock first
                    throw new iPhoneException("iPhone is locked. Please unlock and try again", iPhoneErrorType.DeviceLocked);
                }
                else
                {
                    throw new iPhoneException($"Pairing failed: {lockdownError}", iPhoneErrorType.PairingFailed);
                }
            }
            finally
            {
                lockdownHandle?.Dispose();
                deviceHandle?.Dispose();
            }
        });
    }

    public async Task<bool> IsPairedAsync(string udid)
    {
        // WHY: Check if pairing file exists locally (faster than querying device)
        var pairingFilePath = GetPairingRecordPath(udid);
        if (!File.Exists(pairingFilePath))
        {
            return false;
        }

        // WHY: Verify pairing record is still valid (not just that file exists)
        return await ValidatePairingAsync(udid);
    }

    public async Task<bool> ValidatePairingAsync(string udid)
    {
        return await Task.Run(() =>
        {
            iDeviceHandle deviceHandle = null;
            LockdownClientHandle lockdownHandle = null;

            try
            {
                var ideviceError = LibiMobileDevice.Instance.iDevice.idevice_new(out deviceHandle, udid);
                if (ideviceError != iDeviceError.Success)
                {
                    return false;
                }

                // WHY: Try handshake with existing pairing record
                // If record is invalid (iPhone reset), this will fail
                var lockdownError = LibiMobileDevice.Instance.Lockdown.lockdownd_client_new_with_handshake(
                    deviceHandle,
                    out lockdownHandle,
                    "iPhoneTransfer"
                );

                // WHY: Success = pairing is valid, any error = invalid/expired
                return lockdownError == LockdownError.Success;
            }
            finally
            {
                lockdownHandle?.Dispose();
                deviceHandle?.Dispose();
            }
        });
    }

    /// <summary>
    /// Get the file path where Windows stores the pairing record for this device.
    /// WHY: Check existence, delete stale records, debug pairing issues.
    /// </summary>
    private string GetPairingRecordPath(string udid)
    {
        // WHY: Standard location used by iTunes and libimobiledevice
        var lockdownDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Apple",
            "Lockdown"
        );

        return Path.Combine(lockdownDir, $"{udid}.plist");
    }

    /// <summary>
    /// Get or create a Lockdown client for a device.
    /// WHY: Reuse existing connection instead of creating new one each time (faster).
    /// </summary>
    internal LockdownClientHandle GetLockdownClient(string udid)
    {
        if (_activeLockdownClients.TryGetValue(udid, out var existingClient))
        {
            // WHY: Check if handle is still valid (device might have been disconnected/reconnected)
            if (!existingClient.IsInvalid && !existingClient.IsClosed)
            {
                return existingClient;
            }
            else
            {
                // WHY: Handle became invalid, remove from cache
                _activeLockdownClients.Remove(udid);
            }
        }

        // WHY: Create new Lockdown client
        iDeviceHandle deviceHandle;
        var ideviceError = LibiMobileDevice.Instance.iDevice.idevice_new(out deviceHandle, udid);
        if (ideviceError != iDeviceError.Success)
        {
            throw new iPhoneException("Device not found", iPhoneErrorType.DeviceNotFound) { DeviceUDID = udid };
        }

        LockdownClientHandle lockdownHandle;
        var lockdownError = LibiMobileDevice.Instance.Lockdown.lockdownd_client_new_with_handshake(
            deviceHandle,
            out lockdownHandle,
            "iPhoneTransfer"
        );

        deviceHandle.Dispose(); // WHY: Lockdown client now owns the connection

        if (lockdownError != LockdownError.Success)
        {
            throw new iPhoneException(
                $"Failed to create Lockdown client: {lockdownError}",
                lockdownError == LockdownError.InvalidHostID ? iPhoneErrorType.InvalidPairingRecord : iPhoneErrorType.ServiceUnavailable
            ) { DeviceUDID = udid };
        }

        _activeLockdownClients[udid] = lockdownHandle;
        return lockdownHandle;
    }

    public void Dispose()
    {
        // WHY: Stop background watcher to prevent timer callbacks after disposal
        _deviceWatcher?.Stop();
        _deviceWatcher?.Dispose();

        // WHY: Clean up all active Lockdown connections
        foreach (var client in _activeLockdownClients.Values)
        {
            client?.Dispose();
        }
        _activeLockdownClients.Clear();
    }
}
