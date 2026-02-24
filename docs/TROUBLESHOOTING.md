# Troubleshooting Guide

## Common Errors and Solutions

### 1. "Apple Mobile Device Support is not installed"

**Error Message:**
```
Failed to load Apple Mobile Device Support libraries. 
Please install iTunes or Apple Devices app.
```

**Root Cause:** usbmuxd.dll and related Apple libraries not found.

**WHY This Happens:**
- iTunes not installed
- iTunes installed in non-standard location
- Apple Mobile Device service is disabled
- Incomplete iTunes installation

**Solutions:**

1. **Install iTunes** (simplest solution):
   ```
   Download from: https://www.apple.com/itunes/download/
   ```
   - After installation, you can uninstall iTunes itself
   - Keep "Apple Mobile Device Support" when uninstalling
   - Location: `C:\Program Files\Common Files\Apple\Mobile Device Support\`

2. **Verify Installation:**
   ```powershell
   # Check if files exist
   Test-Path "C:\Program Files\Common Files\Apple\Mobile Device Support\usbmuxd.exe"
   Test-Path "C:\Program Files\Common Files\Apple\Apple Application Support\libimobiledevice.dll"
   ```

3. **Check Windows Service:**
   ```powershell
   Get-Service "Apple Mobile Device Service"
   # Should show Status: Running
   ```

4. **Start Service if Stopped:**
   ```powershell
   Start-Service "Apple Mobile Device Service"
   ```

5. **Alternative: Install Apple Devices (Windows 11):**
   - Available in Microsoft Store
   - Lighter than iTunes
   - Provides same drivers

---

### 2. "Device not found" (iPhone is plugged in)

**Symptoms:**
- iPhone connected via USB
- App shows "No iPhone connected"
- iPhone charges but not detected

**Root Causes & Solutions:**

#### A. Cable is Charge-Only
**WHY:** Many cheap cables only have power pins, no data pins.

**Test:**
```
Plug iPhone into computer
Open File Explorer → This PC
Look for "Apple iPhone" device
```

**Solution:** Use official Apple cable or certified MFi cable.

---

#### B. USB Driver Not Installed

**Test:**
```powershell
# Open Device Manager
devmgmt.msc

# Look for "Apple iPhone" under "Portable Devices"
# OR "Unknown Device" under "Other Devices"
```

**Solution:**
1. Uninstall device in Device Manager
2. Unplug iPhone
3. Restart computer
4. Plug iPhone back in
5. Windows will reinstall drivers

---

#### C. usbmuxd Service Not Running

**Test:**
```powershell
Get-Service "Apple Mobile Device Service"
```

**Solution:**
```powershell
# Set to automatic start
Set-Service "Apple Mobile Device Service" -StartupType Automatic

# Start service
Start-Service "Apple Mobile Device Service"
```

---

#### D. USB Port Issues

**WHY:** Some USB hubs or front panel USB ports don't provide enough power or data bandwidth.

**Solution:**
- Try different USB port (prefer rear motherboard ports)
- Bypass USB hubs
- Use USB 3.0 port if available

---

### 3. "Pairing failed" / "User denied pairing"

**Error Message:**
```
Failed to pair with your iPhone. Make sure you tap 'Trust' and enter your passcode.
```

**Root Causes:**

#### A. User Tapped "Don't Trust"
**Solution:** 
1. Disconnect iPhone
2. On iPhone: Settings → General → Reset → Reset Location & Privacy
3. Reconnect iPhone
4. Tap "Trust" when prompted
5. Enter passcode

---

#### B. iPhone is Locked
**WHY:** Pairing requires unlocked iPhone (security feature).

**Solution:** Unlock iPhone before pairing.

---

#### C. Timeout (User took too long to respond)
**WHY:** Trust dialog times out after 30 seconds.

**Solution:** 
- Disconnect and reconnect
- Be ready to tap "Trust" immediately

---

#### D. Stale Pairing Record

**Symptoms:**
- Previously worked
- iPhone was reset or "Erase All Content and Settings"
- Error: "Invalid pairing record"

**WHY:** Windows has old pairing record, iPhone has forgotten it.

**Solution:**
```powershell
# Delete pairing records
Remove-Item "C:\ProgramData\Apple\Lockdown\*.plist"

# Reconnect iPhone and re-pair
```

---

### 4. "iPhone is locked. Please unlock your iPhone"

**Error Message:**
```
Your iPhone is locked. Please unlock your iPhone and try again.
```

**WHY:** AFC service requires unlocked device (Apple security policy).

**Root Causes:**

#### A. Obvious: iPhone is actually locked
**Solution:** Unlock iPhone.

---

#### B. Auto-Lock Triggered During Operation
**WHY:** iPhone auto-locks after 30 seconds (default).

**Solution:**
```
On iPhone:
Settings → Display & Brightness → Auto-Lock → Never (while transferring)

OR

Keep tapping screen during long transfers
```

---

#### C. Low Power Mode
**WHY:** Low Power Mode can interfere with USB communication.

**Solution:**
```
On iPhone:
Settings → Battery → Low Power Mode → OFF
```

---

### 5. "AFC service unavailable"

**Error Message:**
```
Failed to start AFC service: [error code]
```

**Root Causes:**

#### A. iPhone Not Trusted
**WHY:** AFC requires valid pairing record.

**Solution:** Complete pairing first (see Error #3).

---

#### B. iOS Version Too Old
**WHY:** iOS 7+ required for modern AFC protocol.

**Solution:** Update iPhone to iOS 10 or later.

---

#### C. iOS Developer Mode (iOS 16+)
**WHY:** iOS 16+ requires Developer Mode for some services.

**Test:**
```
On iPhone:
Settings → Privacy & Security → Developer Mode
```

**Solution:** Enable Developer Mode (usually NOT needed for AFC, but worth trying).

---

#### D. Corrupted iOS Installation
**Symptoms:**
- Other apps (iTunes, 3uTools) also can't access files
- Random disconnections

**Solution:**
1. Backup iPhone
2. Restore iPhone via iTunes/Finder
3. Restore from backup

---

### 6. "Out of memory" During Transfer

**Symptoms:**
- App crashes when transferring large videos
- Error: "OutOfMemoryException"

**Root Causes:**

#### A. Bug: Loading Entire File into RAM
**WHY:** Code is using `ReadAllBytes()` instead of streaming.

**Check Code:**
```csharp
// ❌ BAD
byte[] data = afcClient.ReadAllBytes(filePath);

// ✅ GOOD
using var stream = afcClient.OpenRead(filePath);
```

**Solution:** Ensure FileTransferService uses streaming (already implemented).

---

#### B. 32-bit Process Limitation
**WHY:** 32-bit .NET apps limited to ~2GB memory.

**Check:**
```xml
<!-- In .csproj -->
<Platforms>x64</Platforms>
```

**Solution:** Build as 64-bit (already configured).

---

#### C. Insufficient System RAM
**WHY:** PC has less RAM than video file size.

**Solution:**
- Close other applications
- Upgrade system RAM
- Transfer smaller batches

---

### 7. Transfer Speed is Very Slow (< 5 MB/s)

**Symptoms:**
- Transfer shows 2-5 MB/s
- Expected 40+ MB/s on USB 2.0

**Root Causes:**

#### A. USB 2.0 Cable
**WHY:** Most iPhone cables are USB-A to Lightning = USB 2.0.

**Test:**
```
Check cable connector:
- USB-A (rectangular) = USB 2.0 max
- USB-C (oval) = USB 3.0+ possible
```

**Solution:**
- Use Lightning to USB 3 Camera Adapter + USB-C cable
- Accept slower speed (USB 2.0 is still reasonably fast)

---

#### B. USB Hub Sharing Bandwidth
**WHY:** Multiple devices on hub share bandwidth.

**Solution:** Connect iPhone directly to motherboard USB port.

---

#### C. Disk Write Speed Bottleneck
**WHY:** Destination drive (HDD, network drive) is slow.

**Test:**
```powershell
# Check disk write speed
winsat disk -drive C
```

**Solution:**
- Transfer to SSD instead of HDD
- Avoid network drives
- Ensure disk not nearly full

---

#### D. Antivirus Scanning Each File
**WHY:** Real-time antivirus scans files as written.

**Test:** Disable antivirus temporarily, retry transfer.

**Solution:**
- Add destination folder to antivirus exclusions
- Disable real-time scanning during transfer

---

### 8. "File not found" (file shows in list but fails to transfer)

**Symptoms:**
- File appears in scan results
- Transfer fails with "File not found on iPhone"

**Root Causes:**

#### A. iCloud Photo Optimization
**WHY:** Photo stored in iCloud, not on device.

**Check:**
```
On iPhone:
Settings → Photos → Optimize iPhone Storage
```

**Solution:**
```
Settings → Photos → Download and Keep Originals

Wait for photos to download from iCloud
(requires Wi-Fi and sufficient storage)
```

---

#### B. File Deleted Between Scan and Transfer
**WHY:** User deleted photo on iPhone after scan.

**Solution:** Re-scan photos.

---

#### C. Corrupted DCIM Database
**Symptoms:**
- Some files transfer, others don't
- Random pattern

**Solution:**
```
On iPhone:
1. Backup photos to computer/iCloud
2. Settings → General → Reset → Reset All Settings
3. Re-scan
```

---

### 9. HEIC Files Won't Open on Windows

**Symptoms:**
- Files transfer successfully
- Windows says "Can't open this file"
- File extension: .HEIC or .HEIF

**WHY:** Windows 10/11 don't include HEIC codec by default.

**Solutions:**

#### Option 1: Install HEIF/HEVC Codecs
```
Microsoft Store:
1. "HEIF Image Extensions" (free)
2. "HEVC Video Extensions" ($0.99 or free from device manufacturer)
```

#### Option 2: Convert to JPEG During Transfer
**Implementation:** Add to FileTransferService:
```csharp
// Detect HEIC, convert to JPEG using ImageSharp
if (Path.GetExtension(file.FileName).Equals(".heic", StringComparison.OrdinalIgnoreCase))
{
    using var image = Image.Load(sourceStream);
    var jpegPath = Path.ChangeExtension(destinationPath, ".jpg");
    image.Save(jpegPath, new JpegEncoder());
}
```

#### Option 3: Use Third-Party Viewer
- IrfanView (with HEIC plugin)
- XnView
- Google Photos app for Windows

---

### 10. Live Photos Transfer as Separate Files

**Symptoms:**
- IMG_0001.HEIC and IMG_0001.MOV both appear
- Not recognized as Live Photo on Windows

**WHY:** Windows has no native Live Photo support.

**Expected Behavior:** This is normal.

**Workarounds:**

1. **Import to Google Photos:**
   - Maintains Live Photo association
   - Requires cloud upload

2. **Keep Both Files:**
   - Windows Photos app shows both
   - MOV is the "Live" portion

3. **Future Enhancement:**
   - Combine into Windows Live Photo format
   - Requires custom metadata

---

## Debugging Techniques

### Enable Debug Logging

```csharp
// In DeviceManager constructor
Environment.SetEnvironmentVariable("LIBIMD_DEBUG", "1");

// Outputs to Console window
```

**WHY:** See raw AFC protocol messages, detect where communication fails.

---

### Monitor USB Traffic

**Tool:** USB Packet Analyzer (Wireshark with USBPcap)

1. Install Wireshark + USBPcap
2. Capture on iPhone's USB port
3. Filter: `usb.transfer_type == 0x02` (bulk transfers)

**WHY:** See if data is flowing (vs. stuck in protocol handshake).

---

### Check Pairing Record Contents

```powershell
# View pairing record
$udid = "YOUR-IPHONE-UDID"
Get-Content "C:\ProgramData\Apple\Lockdown\$udid.plist"
```

**Look for:**
- HostPrivateKey
- DeviceCertificate
- RootCertificate

**WHY:** Ensure pairing record is complete, not corrupted.

---

### Test with Known-Good Tool

**Before reporting bug, test with:**
- iTunes (can it see iPhone?)
- 3uTools (can it browse files?)
- iMazing (commercial tool)

**WHY:** Isolate app bug vs. system/driver issue.

---

## Error Code Reference

### AFC Error Codes

| Code | Name | Meaning | Solution |
|------|------|---------|----------|
| 0 | SUCCESS | Operation succeeded | N/A |
| 1 | UNKNOWN_ERROR | Generic failure | Check device connection |
| 2 | OP_HEADER_INVALID | Corrupted packet | Retry operation |
| 3 | NO_RESOURCES | Out of memory/handles | Restart app |
| 4 | READ_ERROR | Can't read file | Check file permissions |
| 5 | WRITE_ERROR | Can't write file | Check disk space |
| 6 | UNKNOWN_PACKET_TYPE | Protocol mismatch | Update iOS or app |
| 7 | INVALID_ARG | Bad parameter | App bug, report |
| 8 | OBJECT_NOT_FOUND | File doesn't exist | File deleted or iCloud |
| 9 | OBJECT_IS_DIR | Expected file, got folder | App bug |
| 10 | PERM_DENIED | Access denied | Unlock iPhone, re-pair |
| 11 | SERVICE_NOT_CONNECTED | AFC service stopped | Restart operation |
| 12 | OP_TIMEOUT | Operation took too long | Check USB cable |
| 13 | TOO_MUCH_DATA | File too large | Split transfer |
| 14 | END_OF_DATA | Normal end of file | N/A (not an error) |

---

### Lockdown Error Codes

| Code | Name | Solution |
|------|------|----------|
| 0 | SUCCESS | N/A |
| -1 | INVALID_ARG | App bug |
| -2 | INVALID_CONF | Corrupted config, delete pairing record |
| -3 | PLIST_ERROR | Pairing record corrupted |
| -4 | PAIRING_FAILED | User denied trust |
| -5 | SSL_ERROR | Certificate issue, re-pair |
| -6 | DICT_ERROR | Protocol error |
| -7 | NOT_ENOUGH_DATA | Timeout |
| -8 | MUX_ERROR | usbmuxd crashed |
| -9 | NO_RUNNING_SESSION | Device disconnected |
| -10 | INVALID_RESPONSE | iOS version incompatible |
| -11 | MISSING_KEY | Pairing record incomplete |
| -12 | MISSING_VALUE | Pairing record incomplete |
| -13 | GET_PROHIBITED | Permission denied |
| -14 | SET_PROHIBITED | Permission denied |
| -15 | REMOVE_PROHIBITED | Permission denied |
| -16 | IMMUTABLE_VALUE | Can't change setting |
| -17 | PASSWORD_PROTECTED | iPhone is locked |
| -18 | USER_DENIED_PAIRING | User tapped "Don't Trust" |
| -19 | PAIRING_DIALOG_PENDING | Waiting for user response |
| -20 | MISSING_HOST_ID | No pairing record |
| -21 | INVALID_HOST_ID | Pairing record invalid (iPhone was reset) |
| -22 | SESSION_ACTIVE | Already connected |
| -23 | SESSION_INACTIVE | Not connected |
| -24 | MISSING_SESSION_ID | Internal error |
| -25 | INVALID_SESSION_ID | Internal error |
| -26 | MISSING_SERVICE | Service name wrong |
| -27 | INVALID_SERVICE | Service unavailable |
| -28 | SERVICE_LIMIT | Too many connections |
| -29 | MISSING_PAIR_RECORD | Need to pair first |
| -30 | SAVE_PAIR_RECORD_FAILED | Can't write to C:\ProgramData |
| -31 | INVALID_PAIR_RECORD | Corrupted record |
| -32 | INVALID_ACTIVATION_RECORD | Activation issue |
| -33 | MISSING_ACTIVATION_RECORD | iPhone not activated |
| -34 | SERVICE_PROHIBITED | Service disabled on iPhone |
| -35 | ESCROW_LOCKED | Encrypted backup password needed |
| -36 | PAIRING_PROHIBITED_OVER_THIS_CONNECTION | Try different USB port |

---

## Performance Benchmarks

### Expected Transfer Speeds

| Connection | Theoretical | Real-World | Time for 1GB |
|------------|------------|------------|--------------|
| USB 2.0 | 480 Mbps | 30-40 MB/s | ~30 seconds |
| USB 3.0 | 5 Gbps | 150-200 MB/s | ~6 seconds |
| USB 3.1 | 10 Gbps | 300-400 MB/s | ~3 seconds |
| Wi-Fi Sync | Varies | 5-15 MB/s | ~90 seconds |

**WHY Slower Than Theoretical:**
- Protocol overhead (AFC packets, checksums)
- iPhone I/O speed (NAND flash read speed)
- Windows I/O speed (disk write speed)
- CPU encryption/decryption overhead

---

## When to Report a Bug

**Before reporting:**
1. ✅ Tested with different USB cable
2. ✅ Tested with different USB port
3. ✅ iPhone unlocked and trusted
4. ✅ iTunes can see the device
5. ✅ Reproduced error multiple times
6. ✅ Checked this troubleshooting guide

**Information to include:**
- Windows version (run `winver`)
- iOS version (Settings → General → About)
- iPhone model
- App version
- Full error message
- Steps to reproduce
- Debug logs (if available)

---

## FAQ

### Q: Can I use this app without iTunes installed?

**A:** No. You need iTunes OR "Apple Devices" app (Windows 11) for drivers. However, you can uninstall iTunes itself after installation, keeping only "Apple Mobile Device Support" component.

---

### Q: Does this work with iPad?

**A:** Yes! iPad uses the same AFC protocol. The app will detect it as a device.

---

### Q: Can I transfer photos TO the iPhone?

**A:** No. AFC is read-only for the DCIM folder (security restriction). Use iTunes/Finder for photo sync, or AirDrop.

---

### Q: Why don't some photos from my iPhone appear?

**A:** If "Optimize iPhone Storage" is enabled in Settings → Photos, some photos are stored in iCloud and only thumbnails are on the device. Change to "Download and Keep Originals".

---

### Q: Can I delete photos from iPhone after transfer?

**A:** Not through this app (AFC is read-only). Use Photos app on iPhone or iTunes sync.

---

### Q: Will this work on Windows 7?

**A:** .NET 6 requires Windows 10 or later. You would need to rebuild for .NET Framework 4.8 for Windows 7 support.

---

### Q: Is this legal?

**A:** Yes for personal use. It's reverse engineering for interoperability, which is legal in most jurisdictions. However, Apple could change protocols in future iOS updates, breaking the app.

---

### Q: Can I distribute this app?

**A:** Personal use only. Distributing would require:
- Bundling Apple DLLs (license violation)
- Navigating Apple's private API restrictions
- Ongoing maintenance for iOS updates

---

## Additional Resources

- **libimobiledevice Wiki:** https://github.com/libimobiledevice/libimobiledevice/wiki
- **imobiledevice-net Repo:** https://github.com/libimobiledevice-win32/imobiledevice-net
- **Apple USB Vendor ID:** 0x05AC (for driver troubleshooting)
- **AFC Protocol Spec:** (Reverse-engineered, see libimobiledevice source)

---

**Last Updated:** February 2026
**Tested with:** iOS 15-17, Windows 10/11
