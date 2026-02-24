# iPhone Photo Transfer - Windows Desktop Application

## Overview
This Windows desktop application transfers photos and videos from an iPhone to a Windows PC via USB cable using Apple's private MobileDevice APIs. Built for **personal use only**, it preserves original quality (HEIC, MOV, Live Photos) without re-encoding or cloud dependency.

## System Architecture

### Why This Architecture?

The application is structured in three layers:

1. **iPhoneTransfer.Core**: Business logic and Apple API wrappers
   - **WHY**: Separates platform-specific code from UI, enabling testing and future UI changes
   
2. **iPhoneTransfer.Services**: Device communication and file operations
   - **WHY**: Encapsulates stateful operations (connections, transfers) with lifecycle management
   
3. **iPhoneTransfer.UI**: WPF desktop interface
   - **WHY**: MVVM pattern for responsive UI during long-running transfers

### Technology Stack

| Component | Choice | Reason |
|-----------|--------|--------|
| **Language** | C# .NET 6+ | Modern async/await, cross-platform libraries, strong typing |
| **UI Framework** | WPF | Native Windows, hardware acceleration, rich controls |
| **iPhone Library** | imobiledevice-net | Pure C# wrapper over libimobiledevice, actively maintained |
| **Image Library** | ImageSharp | HEIC thumbnail generation without codec dependencies |

### Why NOT iTunes Library?

iTunes (Apple Mobile Device Support) provides **only drivers** (usbmuxd). We avoid:
- ❌ iTunes UI/database coupling
- ❌ Automatic sync interference  
- ❌ Hidden file conversions

Instead, we use **direct MobileDevice APIs** for:
- ✅ Raw file access
- ✅ No background processes
- ✅ Full control over pairing/transfer

## Apple MobileDevice API Flow

### 1. Connection Establishment (Lockdown Protocol)

```
Windows USB Stack
    ↓
usbmuxd (from iTunes drivers)
    ↓
TCP Tunnel (127.0.0.1:random_port ↔ iPhone service)
    ↓
Lockdown Service (iPhone port 62078)
    ↓
Pairing Record Exchange
    ↓
SSL/TLS Session
    ↓
Service Start Request
```

**WHY each step:**

- **usbmuxd**: Multiplexes multiple services over one USB connection (photos, files, diagnostics all share the cable)
- **Lockdown**: Acts as iPhone's "security bouncer" - validates pairing, issues SSL certificates
- **Pairing Record**: Contains cryptographic proof of "Trust This Computer" - without it, iPhone denies all requests
- **SSL/TLS**: All communication is encrypted even over USB (Apple's security model)

### 2. Photos Service Access Flow

```
1. StartService("com.apple.afc") → AFC (Apple File Conduit)
   WHY: Base filesystem service, but limited to media directories

2. Navigate to /DCIM/100APPLE/
   WHY: Standard iOS camera roll path (DCIM = Digital Camera Images)

3. Alternative: StartService("com.apple.mobile.house_arrest")
   WHY: For app-sandboxed photos (not needed for camera roll)

4. Read file metadata via AFC protocol
   WHY: Get file size, dates WITHOUT downloading entire file

5. Transfer file via AFC read chunks
   WHY: Stream large videos without memory overflow
```

### 3. Pairing ("Trust This Computer") Process

**User sees:**
1. "Trust This Computer?" on iPhone
2. Enters passcode
3. Confirms trust

**Behind the scenes:**
```
1. Windows requests pairing
   ↓
2. iPhone generates RSA keypair
   ↓
3. iPhone sends public key + device certificate
   ↓
4. Windows stores pairing record in:
   C:\ProgramData\Apple\Lockdown\[UDID].plist
   ↓
5. Future connections skip prompt (until "Forget This Computer")
```

**WHY pairing is mandatory:**
- Prevents USB attacks (malicious chargers)
- User must physically unlock phone
- Cryptographic proof prevents MITM attacks

## Project Structure

```
file_transfere_app/
├── src/
│   ├── iPhoneTransfer.Core/           # Core business logic
│   │   ├── Models/
│   │   │   ├── MediaFile.cs           # Photo/video metadata
│   │   │   ├── DeviceInfo.cs          # iPhone details
│   │   │   └── TransferProgress.cs    # Progress tracking
│   │   ├── Interfaces/
│   │   │   ├── IDeviceService.cs
│   │   │   ├── IPhotoService.cs
│   │   │   └── ITransferService.cs
│   │   └── Exceptions/
│   │       └── iPhoneException.cs
│   │
│   ├── iPhoneTransfer.Services/        # Apple API wrappers
│   │   ├── DeviceManager.cs            # USB detection, pairing
│   │   ├── LockdownService.cs          # Lockdown protocol
│   │   ├── AFCService.cs               # File system access
│   │   ├── PhotoLibraryService.cs      # Photo enumeration
│   │   └── FileTransferService.cs      # Actual file copy
│   │
│   └── iPhoneTransfer.UI/              # WPF Application
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   └── PhotoGridViewModel.cs
│       ├── Views/
│       │   └── MainWindow.xaml
│       ├── Converters/
│       │   └── FileSizeConverter.cs
│       └── App.xaml
│
├── docs/
│   ├── ARCHITECTURE.md                 # This file (detailed)
│   ├── TROUBLESHOOTING.md              # Common errors
│   └── API_REFERENCE.md                # MobileDevice API details
│
├── tests/
│   └── iPhoneTransfer.Tests/
│
└── iPhoneTransfer.sln
```

## Prerequisites

### Software Requirements

1. **iTunes or Apple Mobile Device Support**
   - **WHY**: Provides usbmuxd.exe and USB drivers
   - **REQUIRED FILES**:
     - `C:\Program Files\Common Files\Apple\Mobile Device Support\usbmuxd.exe`
     - `C:\Program Files\Common Files\Apple\Apple Application Support\libimobiledevice.dll`
   - **HOW**: Install iTunes (can be uninstalled after drivers remain)

2. **.NET 6 SDK or later**
   - Download: https://dot.net

3. **Visual Studio 2022** (recommended)
   - Workloads: ".NET Desktop Development"

### NuGet Packages

```xml
<!-- iPhoneTransfer.Services.csproj -->
<ItemGroup>
  <!-- Core iPhone communication -->
  <PackageReference Include="imobiledevice-net" Version="1.3.17" />
  
  <!-- HEIC thumbnail generation -->
  <PackageReference Include="SixLabors.ImageSharp" Version="3.0.0" />
  
  <!-- Modern async utilities -->
  <PackageReference Include="System.Linq.Async" Version="6.0.1" />
</ItemGroup>
```

**WHY these specific packages:**

- **imobiledevice-net**: 
  - Pure C# (no P/Invoke complexity)
  - Handles usbmuxd communication
  - Implements Lockdown, AFC protocols
  - Active maintenance (critical for iOS updates)

- **ImageSharp**: 
  - Supports HEIC decoding (Windows doesn't natively)
  - Cross-platform (future macOS port possible)
  - Faster than WIC for thumbnails

## Key Design Decisions

### 1. Why AFC Instead of libplist?

**AFC (Apple File Conduit)** is the correct service because:
- ✅ Designed for media file access
- ✅ Handles large files efficiently
- ✅ Respects iOS permissions

**libplist** is only for:
- Property list parsing
- System configuration files
- NOT for binary data transfer

### 2. Why NOT Use Photos.app Database?

iOS stores photos in:
- `/var/mobile/Media/PhotoData/Photos.sqlite`

**Problems:**
- ❌ Requires jailbreak or root access
- ❌ Database schema changes per iOS version
- ❌ Deleted photos still in DB
- ❌ Violates Apple's security model

**Our approach:**
- ✅ Use AFC to browse `/DCIM` (standard camera path)
- ✅ Works on stock iOS
- ✅ Future-proof (DCIM is unchanging standard)

### 3. Why Store Pairing Records?

**Location**: `C:\ProgramData\Apple\Lockdown\[UDID].plist`

**WHY:**
- User experience: Trust once, use forever
- Security: Pairing record = cryptographic proof
- Compatibility: Same location iTunes uses (no conflicts)

**NEVER:**
- ❌ Store in user temp folder (gets cleaned)
- ❌ Include in source control (contains private keys)
- ❌ Share between computers (defeats trust purpose)

## Common Errors and Root Causes

### Error: "Device not found" (even though plugged in)

**Root cause**: usbmuxd not running or wrong path

**WHY this happens:**
- iTunes installed in non-default location
- Apple Mobile Device service stopped
- USB cable is charge-only (no data pins)

**Debug steps:**
```powershell
# Check if usbmuxd is accessible
Test-Path "C:\Program Files\Common Files\Apple\Mobile Device Support\usbmuxd.exe"

# Check service status
Get-Service | Where-Object {$_.Name -like "*Apple*"}

# Expected: "Apple Mobile Device Service" = Running
```

**Fix in code:**
```csharp
// Auto-locate usbmuxd (don't hardcode paths)
var usbmuxdPath = Environment.GetEnvironmentVariable("USBMUXD_PATH") 
    ?? @"C:\Program Files\Common Files\Apple\Mobile Device Support\usbmuxd.exe";
```

### Error: "Pairing failed" / "User denied trust"

**Root cause**: User didn't tap "Trust" OR old pairing record exists

**WHY this happens:**
- User tapped "Don't Trust"
- Phone was reset (pairing records deleted on iPhone)
- Windows clock is wrong (SSL cert validation fails)

**Debug:**
```csharp
// Check if pairing record exists
var pairingFile = $@"C:\ProgramData\Apple\Lockdown\{device.UDID}.plist";
if (!File.Exists(pairingFile)) {
    // User needs to trust - expected
} else {
    // Pairing record exists but still failing
    // → iPhone was reset, delete stale record
    File.Delete(pairingFile);
}
```

### Error: "AFC service unavailable"

**Root cause**: iPhone is locked or wrong service name

**WHY:**
- iPhone must be **unlocked** for AFC access (security feature)
- Service name typo: "com.apple.afc" NOT "com.apple.afc2"

**Fix:**
```csharp
// Always check device lock state before AFC
if (device.IsLocked) {
    throw new iPhoneException("Please unlock your iPhone");
}

// Use correct service name
const string AFC_SERVICE = "com.apple.afc";  // For media files
// NOT "com.apple.afc2" (that's for full filesystem on jailbroken devices)
```

### Error: "Out of memory" when transferring large videos

**Root cause**: Loading entire file into memory

**WHY this is wrong:**
```csharp
// ❌ BAD: Loads 2GB video into RAM
byte[] fileData = afcClient.ReadAllBytes(filePath);
File.WriteAllBytes(destination, fileData);
```

**Correct approach:**
```csharp
// ✅ GOOD: Stream in 1MB chunks
using var afcStream = afcClient.OpenRead(filePath);
using var fileStream = File.Create(destination);
await afcStream.CopyToAsync(fileStream, bufferSize: 1024 * 1024);
```

## Performance Considerations

### Why USB 2.0 vs USB 3.0 Matters

| Transfer | USB 2.0 Speed | USB 3.0 Speed |
|----------|--------------|---------------|
| 1GB video | ~30 seconds | ~5 seconds |
| 100 photos (500MB) | ~15 seconds | ~3 seconds |

**WHY the difference:**
- USB 2.0: 480 Mbps theoretical (40 MB/s real-world)
- USB 3.0: 5 Gbps theoretical (200+ MB/s real-world)

**Code doesn't change** - bottleneck is cable/port, not software.

**User tip**: Check cable - many iPhone cables are USB 2.0 only.

### Why Parallel Transfers Are Risky

**DON'T:**
```csharp
// ❌ Multiple simultaneous AFC connections
await Task.WhenAll(files.Select(f => TransferFileAsync(f)));
```

**WHY:**
- AFC service is **single-threaded** on iPhone
- Multiple connections cause:
  - Lock contention
  - Connection drops
  - Slower overall speed (overhead > parallelism gain)

**DO:**
```csharp
// ✅ Sequential transfers with progress
foreach (var file in files) {
    await TransferFileAsync(file, progressCallback);
}
```

## Security and Privacy

### What Data Is Accessed?

**READ access:**
- ✅ `/DCIM/` (camera roll photos/videos)
- ✅ File metadata (name, size, date)

**NO access to:**
- ❌ Messages, contacts, call logs
- ❌ App data (unless app explicitly shares via AFC)
- ❌ System files
- ❌ Passwords or keychain

**WHY this is safe:**
- AFC service is **sandboxed** by iOS
- Only media directories are exposed
- User must unlock + trust (dual authentication)

### Pairing Record Security

**What's in a pairing record:**
```xml
<plist>
  <dict>
    <key>HostPrivateKey</key>  <!-- Windows PC's private key -->
    <key>HostCertificate</key> <!-- Windows PC's certificate -->
    <key>DeviceCertificate</key> <!-- iPhone's certificate -->
    <key>RootCertificate</key> <!-- Apple's root CA -->
  </dict>
</plist>
```

**Security implications:**
- 🔒 Private key = proof of trust
- ⚠️ If stolen, attacker can access THIS iPhone from ANY PC
- 🛡️ Mitigated by: iPhone must still be unlocked

**Best practice:**
```csharp
// Ensure pairing records have restricted permissions
var pairingDir = @"C:\ProgramData\Apple\Lockdown";
var dirInfo = new DirectoryInfo(pairingDir);
// Only SYSTEM and Administrators should have access
```

## Testing Strategy

### Unit Tests (Mock iPhone)

```csharp
// Mock usbmuxd responses for CI/CD
public class MockUsbmuxdDevice : IUsbmuxdDevice {
    public string UDID => "FAKE-UDID-FOR-TESTING";
    // ...
}
```

**WHY mock:**
- ✅ CI/CD without physical iPhone
- ✅ Test error paths (disconnect, low battery)
- ✅ Fast iteration

### Integration Tests (Real Device)

```csharp
[Fact, Trait("Category", "RequiresDevice")]
public async Task TransferRealPhoto() {
    var device = await DeviceManager.GetConnectedDevice();
    Assume.That(device != null); // Skip if no iPhone
    // ...
}
```

**WHY separate:**
- Real device tests are slow
- Run manually before release
- Catch iOS version-specific issues

## Future Enhancements (Out of Scope)

### What This App DOESN'T Do (By Design)

1. **iCloud Photos**
   - WHY NOT: Requires Apple ID, cloud API, not "cable-only"
   
2. **Delete Photos from iPhone**
   - WHY NOT: AFC is read-only for safety (prevent accidental data loss)
   
3. **Edit Metadata**
   - WHY NOT: Requires Photos.sqlite access (fragile, version-dependent)
   
4. **Background Service**
   - WHY NOT: User should explicitly initiate transfers (no surprise battery drain)

### Possible Future Features

1. **Live Photos Support**
   - Current: Transfers .MOV + .HEIC separately
   - Future: Detect pairs, copy to Windows Live Photo format
   
2. **Incremental Sync**
   - Current: Manual selection each time
   - Future: Remember last transfer, copy only new photos
   
3. **Video Transcoding**
   - Current: Byte-for-byte copy (HEVC videos may not play on old PCs)
   - Future: Optional H.264 conversion (user-configurable)

## License and Disclaimer

**Personal use only.** This application:
- Uses private Apple APIs (not App Store approved)
- Requires iTunes installation (redistributing Apple binaries is prohibited)
- Provided AS-IS with no warranty

**Legal considerations:**
- ✅ Legal for personal use (reverse engineering for interoperability)
- ❌ NOT for commercial distribution
- ❌ NOT for App Store submission

**Apple's stance:**
- Tolerates personal tools (like libimobiledevice)
- Actively blocks App Store apps using private APIs
- Could change protocols in future iOS updates (maintenance required)

## Support and Maintenance

### When iOS Updates Break This App

**Common breakage points:**
1. AFC protocol changes (rare, last change: iOS 7)
2. New photo formats (e.g., ProRAW, Cinematic Mode)
3. Pairing certificate changes

**How to debug:**
```bash
# Enable libimobiledevice debug logging
set LIMD_DEBUG=1
# Re-run app, check console for protocol errors
```

**Update strategy:**
1. Wait 1-2 weeks after major iOS release
2. Check imobiledevice-net GitHub issues
3. Update NuGet packages
4. Test with new iOS version

### Community Resources

- **imobiledevice-net**: https://github.com/libimobiledevice-win32/imobiledevice-net
- **libimobiledevice docs**: https://www.libimobiledevice.org/
- **Apple protocols (reverse-engineered)**: https://github.com/libimobiledevice/libimobiledevice/wiki

---

## Quick Start

```bash
# Clone repository
git clone <your-repo>
cd file_transfere_app

# Build solution
dotnet build

# Run application
dotnet run --project src/iPhoneTransfer.UI

# First-time setup:
# 1. Connect iPhone via USB
# 2. Unlock iPhone
# 3. Tap "Trust This Computer" when prompted
# 4. App will detect device and show photos
```

**Next steps**: See `docs/ARCHITECTURE.md` for detailed code walkthroughs.
