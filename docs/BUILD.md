# Build and Run Instructions

## Prerequisites

### Required Software

1. **.NET 6 SDK or later**
   ```powershell
   # Check if installed
   dotnet --version
   
   # Should show: 6.0.x or higher
   ```
   Download: https://dotnet.microsoft.com/download

2. **Visual Studio 2022** (Recommended) OR VS Code
   - Workload: ".NET Desktop Development"
   - Alternative: Rider, VS Code with C# extension

3. **iTunes or Apple Devices App**
   - Windows 10/11: Either works
   - Provides Apple Mobile Device Support drivers
   - Download iTunes: https://www.apple.com/itunes/download/

### Verify Apple Drivers

```powershell
# Check if usbmuxd is installed
Test-Path "C:\Program Files\Common Files\Apple\Mobile Device Support\usbmuxd.exe"

# Check if service is running
Get-Service "Apple Mobile Device Service"
```

---

## Building the Project

### Option 1: Visual Studio

```bash
# 1. Open solution
iPhoneTransfer.sln

# 2. Restore NuGet packages (automatic)
# 3. Build → Build Solution (Ctrl+Shift+B)
# 4. Run → Start Debugging (F5)
```

### Option 2: Command Line

```powershell
# Navigate to project root
cd file_transfere_app

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run application
dotnet run --project src/iPhoneTransfer.UI
```

### Option 3: Build for Release

```powershell
# Build optimized release version
dotnet build --configuration Release

# Run release build
dotnet run --project src/iPhoneTransfer.UI --configuration Release
```

---

## Publishing Standalone Executable

### Self-Contained Deployment (Recommended)

```powershell
# Publish with bundled .NET runtime (no .NET installation required)
dotnet publish src/iPhoneTransfer.UI `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output publish/win-x64 `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

# Output: publish/win-x64/iPhoneTransfer.exe (single file)
```

### Framework-Dependent Deployment (Smaller)

```powershell
# Requires .NET 6 runtime on target machine
dotnet publish src/iPhoneTransfer.UI `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  --output publish/win-x64-framework `
  -p:PublishSingleFile=true

# Output: publish/win-x64-framework/iPhoneTransfer.exe (~15 MB vs ~70 MB self-contained)
```

---

## First Run Checklist

1. **Connect iPhone via USB**
   - Use official Apple cable or certified MFi cable
   - Plug into rear motherboard USB port (not hub)

2. **Unlock iPhone**
   - Enter passcode
   - Keep screen on

3. **Trust Computer**
   - Tap "Trust" when prompted on iPhone
   - Enter passcode again

4. **Run Application**
   ```powershell
   dotnet run --project src/iPhoneTransfer.UI
   ```

5. **Expected Behavior:**
   - App shows "Connected to [Your iPhone Name]"
   - Click "Scan Photos"
   - Photo grid populates with thumbnails
   - Select files and click "Transfer Selected Files"

---

## Troubleshooting Build Issues

### "Package 'imobiledevice-net' not found"

```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Rebuild
dotnet build
```

### "Could not load file or assembly 'libimobiledevice'"

**Cause:** Native DLLs not found

**Solution:** Ensure iTunes installed or add DLLs to output directory:
```xml
<!-- Add to iPhoneTransfer.Services.csproj -->
<ItemGroup>
  <None Include="path\to\libimobiledevice.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### "The type or namespace name 'CommunityToolkit' could not be found"

```powershell
# Install missing package
dotnet add src/iPhoneTransfer.UI package CommunityToolkit.Mvvm
```

### Build Succeeds but App Crashes on Startup

1. **Check Application Event Log:**
   ```powershell
   Get-EventLog -LogName Application -Newest 10 | Where-Object {$_.Source -like "*NET Runtime*"}
   ```

2. **Run with Debug Info:**
   ```powershell
   dotnet run --project src/iPhoneTransfer.UI --verbosity detailed
   ```

---

## Development Tips

### Hot Reload (Visual Studio 2022)

- Edit .cs files while debugging
- Changes apply immediately (for most code changes)
- XAML changes also hot-reload

### Debug Output

Add to App.xaml.cs:
```csharp
Environment.SetEnvironmentVariable("LIBIMD_DEBUG", "1"); // libimobiledevice debug logs
```

### Test Without iPhone

Create mock device service:
```csharp
// In MainViewModel constructor
#if DEBUG
    var mockDevice = new DeviceInfo {
        UDID = "TEST-DEVICE",
        DeviceName = "Test iPhone",
        ProductType = "iPhone14,2",
        ProductVersion = "16.0",
        IsPaired = true,
        IsLocked = false
    };
    ConnectedDevice = mockDevice;
#endif
```

---

## Running Tests

```powershell
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~DeviceManagerTests"
```

---

## Project Structure Reference

```
file_transfere_app/
├── src/
│   ├── iPhoneTransfer.Core/          # Business models and interfaces
│   │   ├── Models/                   # DeviceInfo, MediaFile, TransferProgress
│   │   ├── Interfaces/               # IDeviceService, IPhotoService, ITransferService
│   │   └── Exceptions/               # iPhoneException
│   │
│   ├── iPhoneTransfer.Services/      # Implementation layer
│   │   ├── DeviceManager.cs          # Device detection, pairing
│   │   ├── PhotoLibraryService.cs    # Photo enumeration via AFC
│   │   └── FileTransferService.cs    # File transfer logic
│   │
│   └── iPhoneTransfer.UI/            # WPF application
│       ├── ViewModels/               # MVVM view models
│       ├── Views/                    # XAML views
│       └── App.xaml                  # Application entry point
│
├── tests/
│   └── iPhoneTransfer.Tests/         # Unit tests
│
└── docs/
    ├── TROUBLESHOOTING.md            # This file
    └── BUILD.md                      # Build instructions
```

---

## Deployment Checklist

Before distributing to other users:

- [ ] Test on clean Windows 10/11 VM
- [ ] Verify iTunes installation prompt appears if not installed
- [ ] Test with multiple iPhone models
- [ ] Test with iOS 15, 16, 17
- [ ] Include README with iTunes requirement
- [ ] Consider code signing certificate (optional, prevents "Unknown Publisher" warning)

---

## Performance Optimization

### For Faster Builds

```powershell
# Skip tests during build
dotnet build --no-restore /p:RunTests=false

# Parallel build
dotnet build -m:4
```

### For Smaller Executables

```xml
<!-- Add to UI project -->
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>link</TrimMode>
</PropertyGroup>
```

⚠️ **Warning:** Trimming can break reflection-based libraries. Test thoroughly.

---

## License Considerations

**You can:**
- ✅ Use for personal file transfers
- ✅ Modify source code
- ✅ Share with friends/family

**You cannot:**
- ❌ Redistribute Apple DLLs (copyright violation)
- ❌ Publish to App Store (uses private APIs)
- ❌ Sell commercially without proper licensing

**Dependencies:**
- imobiledevice-net: LGPL 2.1
- libimobiledevice: LGPL 2.1
- Your code: Choose your license

---

**Need Help?**
- Check `docs/TROUBLESHOOTING.md` for common issues
- Review `README.md` for architecture details
- Submit issues to repository (if applicable)
