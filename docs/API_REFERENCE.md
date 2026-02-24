# API Reference - Apple MobileDevice Protocol

## Overview

This document details the Apple MobileDevice APIs used in this application, based on reverse-engineered protocols from libimobiledevice.

**WHY this document exists:** Understanding the protocol helps debug issues and extend functionality.

---

## Protocol Stack

```
Application (C#)
    ↓
imobiledevice-net (C# wrapper)
    ↓
libimobiledevice (Native library)
    ↓
usbmuxd (USB multiplexer)
    ↓
Apple Mobile Device USB Driver
    ↓
USB Cable
    ↓
iPhone
```

---

## 1. usbmuxd Protocol

**Purpose:** Multiplexes TCP connections over USB.

**WHY:** Single USB connection supports multiple services (AFC, diagnostics, lockdown, etc.) simultaneously.

### Device List Request

```
Protocol: Binary plist over TCP (port 27015 on localhost)
```

**Request:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN">
<plist version="1.0">
<dict>
    <key>MessageType</key>
    <string>ListDevices</string>
    <key>ProgName</key>
    <string>iPhoneTransfer</string>
</dict>
</plist>
```

**Response:**
```xml
<dict>
    <key>DeviceList</key>
    <array>
        <dict>
            <key>DeviceID</key>
            <integer>42</integer>
            <key>Properties</key>
            <dict>
                <key>SerialNumber</key>
                <string>a1b2c3d4e5f6...</string>
                <key>ConnectionType</key>
                <string>USB</string>
                <key>DeviceID</key>
                <integer>42</integer>
                <key>LocationID</key>
                <integer>336592896</integer>
                <key>ProductID</key>
                <integer>4776</integer>
            </dict>
        </dict>
    </array>
</dict>
```

**WHY DeviceID matters:** Used to establish service connections.

---

### Connect to Service

**Request:**
```xml
<dict>
    <key>MessageType</key>
    <string>Connect</string>
    <key>DeviceID</key>
    <integer>42</integer>
    <key>PortNumber</key>
    <integer>62078</integer>  <!-- Lockdown service port -->
</dict>
```

**Response:**
```xml
<dict>
    <key>MessageType</key>
    <string>Result</string>
    <key>Number</key>
    <integer>0</integer>  <!-- 0 = success -->
</dict>
```

**WHY this is important:** After successful connection, socket becomes direct tunnel to iPhone service.

---

## 2. Lockdown Protocol

**Purpose:** Device authentication, pairing, service brokering.

**Port:** 62078 (on iPhone)

**WHY first step:** All services require Lockdown authorization.

### Query Device Value

**Request:**
```xml
<dict>
    <key>Key</key>
    <string>DeviceName</string>
    <key>Request</key>
    <string>GetValue</string>
</dict>
```

**Response:**
```xml
<dict>
    <key>Value</key>
    <string>John's iPhone</string>
</dict>
```

**Available Keys:**
- `DeviceName` - User-assigned name
- `ProductType` - Model identifier (e.g., "iPhone14,2")
- `ProductVersion` - iOS version
- `UniqueDeviceID` - UDID (40-character hex)
- `PasswordProtected` - Boolean (locked status)
- `BatteryCurrentCapacity` - Integer 0-100
- `BatteryIsCharging` - Boolean

---

### Pair Device

**Request:**
```xml
<dict>
    <key>PairRecord</key>
    <dict>
        <key>HostID</key>
        <string>RANDOM-UUID</string>
        <key>SystemBUID</key>
        <string>SYSTEM-UUID</string>
    </dict>
    <key>Request</key>
    <string>Pair</string>
    <key>ProtocolVersion</key>
    <string>2</string>
</dict>
```

**Response (Success):**
```xml
<dict>
    <key>EscrowBag</key>
    <data>BASE64-ENCODED-DATA</data>
    <key>HostCertificate</key>
    <data>BASE64-ENCODED-CERT</data>
    <key>HostPrivateKey</key>
    <data>BASE64-ENCODED-KEY</data>
    <key>RootCertificate</key>
    <data>BASE64-ENCODED-CERT</data>
</dict>
```

**WHY EscrowBag:** Contains encrypted backup key (not used by this app).

**Pairing Record Storage:**
- Windows: `C:\ProgramData\Apple\Lockdown\{UDID}.plist`
- macOS: `/var/db/lockdown/{UDID}.plist`

---

### Start Service

**Request:**
```xml
<dict>
    <key>Request</key>
    <string>StartService</string>
    <key>Service</key>
    <string>com.apple.afc</string>
</dict>
```

**Response:**
```xml
<dict>
    <key>Port</key>
    <integer>49152</integer>  <!-- Dynamic port number -->
    <key>EnableServiceSSL</key>
    <true/>
</dict>
```

**WHY SSL flag:** Some services require SSL handshake after connection.

**Common Services:**
- `com.apple.afc` - Apple File Conduit (media files)
- `com.apple.afc2` - Root filesystem (jailbroken only)
- `com.apple.mobile.house_arrest` - App sandboxes
- `com.apple.mobile.notification_proxy` - Notifications
- `com.apple.syslog_relay` - System logs
- `com.apple.crashreportcopymobile` - Crash reports

---

## 3. AFC Protocol

**Purpose:** File system access (read, list, stat).

**WHY limited:** iOS sandboxes restrict AFC to `/DCIM` and app-specific paths.

### List Directory

**Request:**
```
Packet structure (binary):
- Magic: "CFA6LPAA" (8 bytes)
- Total length: uint64
- Header length: uint64 (40 bytes)
- Packet ID: uint64 (increments)
- Operation: uint64 (3 = ReadDirectory)
- Data: Path string + null terminator
```

**Response:**
```
- Magic: "CFA6LPAA"
- Status: uint64 (0 = success)
- Data: Null-separated list of filenames
```

**Example (pseudo-code):**
```
Request: ReadDirectory("/DCIM")
Response: ".\0..\0100APPLE\0101APPLE\0"
```

---

### Get File Info

**Operation:** 10 (GetFileInfo)

**Request:**
```
Path: "/DCIM/100APPLE/IMG_0001.HEIC"
```

**Response:**
```
Key-value pairs (null-separated):
"st_size\0"
"2097152\0"
"st_mtime\0"
"1640995200000000000\0"
"st_birthtime\0"
"1640995200000000000\0"
"st_ifmt\0"
"S_IFREG\0"
```

**Key Meanings:**
- `st_size` - File size in bytes
- `st_mtime` - Modification time (nanoseconds since epoch)
- `st_birthtime` - Creation time (nanoseconds since epoch)
- `st_ifmt` - File type (S_IFREG = regular file, S_IFDIR = directory)

**WHY nanoseconds:** iOS uses high-precision timestamps.

**Conversion:**
```csharp
long nanos = 1640995200000000000;
DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000).DateTime;
```

---

### Open File

**Operation:** 13 (FileOpen)

**Mode flags:**
- `1` = Read only
- `2` = Write only
- `3` = Read/write
- `4` = Append

**Request:**
```
Mode: 1 (read)
Path: "/DCIM/100APPLE/IMG_0001.HEIC"
```

**Response:**
```
File handle: uint64 (e.g., 5)
```

**WHY handle:** Used for subsequent read/seek/close operations.

---

### Read File

**Operation:** 15 (FileRead)

**Request:**
```
File handle: 5
Bytes to read: 1048576 (1 MB)
```

**Response:**
```
Status: 0
Data: [binary data]
```

**WHY chunked reading:** Prevents memory overflow for large files.

---

### Close File

**Operation:** 20 (FileClose)

**Request:**
```
File handle: 5
```

**WHY mandatory:** Prevents resource leaks on iPhone.

---

## 4. Operation Codes Reference

### AFC Operations

| Code | Name | Purpose |
|------|------|---------|
| 1 | STATUS | Query last error |
| 2 | DATA | Generic data response |
| 3 | READ_DIR | List directory contents |
| 4 | READ_FILE | Read file (deprecated, use FileRead) |
| 5 | WRITE_FILE | Write file (read-only AFC doesn't support) |
| 6 | WRITE_PART | Partial write |
| 7 | TRUNCATE | Truncate file |
| 8 | REMOVE_PATH | Delete file/folder |
| 9 | MAKE_DIR | Create directory |
| 10 | GET_FILE_INFO | File metadata |
| 11 | GET_DEVINFO | Device info |
| 12 | WRITE_FILE_ATOM | Atomic write |
| 13 | FILE_OPEN | Open file |
| 14 | FILE_OPEN_RES | Open response |
| 15 | FILE_READ | Read from open file |
| 16 | FILE_WRITE | Write to open file |
| 17 | FILE_SEEK | Move file pointer |
| 18 | FILE_TELL | Get file pointer position |
| 19 | FILE_TELL_RES | Tell response |
| 20 | FILE_CLOSE | Close file |
| 21 | FILE_SET_SIZE | Set file size |
| 22 | GET_CON_INFO | Connection info |
| 23 | SET_CON_OPTIONS | Set connection options |
| 24 | RENAME_PATH | Rename/move file |
| 25 | SET_FS_BS | Set filesystem block size |
| 26 | SET_SOCKET_BS | Set socket buffer size |
| 27 | FILE_LOCK | Lock file |
| 28 | MAKE_LINK | Create symbolic link |
| 29 | GET_FILE_HASH | Compute file hash |
| 30 | SET_FILE_MOD_TIME | Set modification time |
| 31 | GET_FILE_HASH_RANGE | Hash portion of file |

---

## 5. Error Handling

### AFC Error Codes

See `docs/TROUBLESHOOTING.md` for complete list.

**Most Common:**
- `0` - Success
- `8` - Object not found (file doesn't exist)
- `10` - Permission denied (device locked or not paired)
- `12` - Operation timeout

---

## 6. Performance Optimization

### Batch Operations

**WHY slow:**
```csharp
// ❌ Opens/closes AFC service for each file
foreach (var file in files) {
    using var afc = StartAFCService();
    TransferFile(afc, file);
}
```

**WHY fast:**
```csharp
// ✅ Reuses AFC service for all files
using var afc = StartAFCService();
foreach (var file in files) {
    TransferFile(afc, file);
}
```

**Speedup:** 10-100x for many small files.

---

### Read Buffer Size

**Tested values:**
- 64 KB: Slow (too many round trips)
- 256 KB: Good for photos
- 1 MB: **Optimal** for most use cases
- 4 MB: Marginal gains, risks timeout

**WHY 1 MB:** Balance between memory usage and USB packet efficiency.

---

## 7. Security Considerations

### Pairing Cryptography

**Algorithm:** RSA-2048 + AES-256

**Process:**
1. iPhone generates RSA keypair
2. PC receives public key
3. All traffic encrypted with session keys
4. Keys derived from pairing record

**WHY secure:** Even over USB, data is encrypted (prevents malicious chargers).

---

### Certificate Chain

```
Apple Root CA
    ↓
Device Certificate (unique per iPhone)
    ↓
Host Certificate (generated during pairing)
```

**WHY trust required:** Without valid certificate chain, iOS rejects all requests.

---

## 8. Protocol Versions

### Lockdown Versions

- **Version 1:** iOS 1.0 - 6.x (deprecated)
- **Version 2:** iOS 7.0+ (current, mandatory)

**Breaking changes in v2:**
- Requires SSL for all services
- Enhanced pairing verification
- New error codes

**WHY matters:** App requires iOS 7+ minimum.

---

## 9. Limitations

### What AFC **Cannot** Do

1. **Write to DCIM** - Read-only access
2. **Delete files** - No delete operation for camera roll
3. **Access system files** - Sandboxed to `/DCIM` and app containers
4. **Modify file attributes** - Can't change permissions, ownership

**WHY:** iOS security model prevents filesystem tampering.

---

### What Lockdown **Cannot** Do

1. **Bypass lock screen** - Requires user passcode
2. **Force pairing** - Requires user tap "Trust"
3. **Access encrypted backups** - Requires backup password

**WHY:** Physical access ≠ data access (security by design).

---

## 10. Reverse Engineering Tools

### Used to Document This Protocol

- **Wireshark + USBPcap** - Capture USB traffic
- **iTunes decompilation** - Study Apple's implementation
- **libimobiledevice source** - Reference implementation
- **iOS filesystem dumps** - Understand file structure

**Legal note:** Reverse engineering for interoperability is legal (DMCA exemption), but distributing Apple code is not.

---

## 11. Future iOS Changes

### Risk Areas

**High risk:**
- Pairing protocol changes (breaks existing apps)
- AFC service deprecation (unlikely, but possible)

**Medium risk:**
- New authentication requirements (e.g., biometric)
- Certificate expiration requirements

**Low risk:**
- DCIM path changes (unchanged since iOS 1.0)
- File format changes (backward compatible)

**Mitigation:**
- Monitor libimobiledevice updates
- Test with iOS betas
- Graceful degradation

---

## Additional Resources

- **libimobiledevice GitHub:** https://github.com/libimobiledevice/libimobiledevice
- **Protocol documentation:** https://www.theiphonewiki.com/wiki/Usbmux
- **AFC internals:** https://www.theiphonewiki.com/wiki/AFC

---

**Last Updated:** February 2026
**Tested with:** iOS 15.0 - 17.3, Windows 10/11
