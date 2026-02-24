using System.Collections.ObjectModel;
using iMobileDevice;
using iMobileDevice.Afc;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iPhoneTransfer.Core.Exceptions;
using iPhoneTransfer.Core.Interfaces;
using iPhoneTransfer.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace iPhoneTransfer.Services;

/// <summary>
/// Service for browsing photos and videos on iPhone via AFC (Apple File Conduit).
/// WHY: AFC is the ONLY official way to access media files on non-jailbroken iPhone.
/// </summary>
public class PhotoLibraryService : IPhotoService
{
    private readonly DeviceManager _deviceManager;

    // WHY: Standard iOS camera roll path - unchanged since iPhone OS 1.0
    private const string DCIM_PATH = "/DCIM";
    
    // WHY: Supported photo formats (HEIC is Apple's default since iOS 11)
    private static readonly string[] PHOTO_EXTENSIONS = { ".heic", ".heif", ".jpg", ".jpeg", ".png", ".gif" };
    
    // WHY: Supported video formats (MOV/HEVC is Apple's default)
    private static readonly string[] VIDEO_EXTENSIONS = { ".mov", ".mp4", ".m4v", ".3gp" };

    public PhotoLibraryService(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    /// <summary>
    /// Get all media files from iPhone's camera roll.
    /// ALGORITHM:
    /// 1. Start AFC service
    /// 2. Recursively scan /DCIM/###APPLE/ folders (100APPLE, 101APPLE, etc.)
    /// 3. Read file metadata for each image/video
    /// 4. Return list (NO thumbnails or file content - too slow)
    /// </summary>
    public async Task<List<MediaFile>> GetAllMediaFilesAsync(
        string udid, 
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var mediaFiles = new List<MediaFile>();
            
            // WHY: Get AFC service handle (requires paired + unlocked device)
            using var afcHandle = StartAFCService(udid);

            // WHY: List all folders in /DCIM (100APPLE, 101APPLE, etc.)
            // Apple creates new folders every 999 photos
            var dcimFolders = ListDirectory(afcHandle, DCIM_PATH);
            
            var fileCount = 0;
            foreach (var folder in dcimFolders)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // WHY: Skip hidden folders and metadata files
                if (folder.StartsWith("."))
                    continue;

                var folderPath = $"{DCIM_PATH}/{folder}";
                
                // WHY: Get all files in this folder (IMG_0001.HEIC, etc.)
                var files = ListDirectory(afcHandle, folderPath);

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var filePath = $"{folderPath}/{file}";
                    var extension = Path.GetExtension(file).ToLowerInvariant();

                    // WHY: Only process known media types
                    if (!PHOTO_EXTENSIONS.Contains(extension) && !VIDEO_EXTENSIONS.Contains(extension))
                        continue;

                    try
                    {
                        // WHY: Get file metadata WITHOUT downloading the file (faster)
                        var fileInfo = GetFileInfo(afcHandle, filePath);
                        
                        var mediaFile = new MediaFile
                        {
                            FilePath = filePath,
                            FileName = file,
                            FileSize = fileInfo.Size,
                            CreatedDate = fileInfo.CreatedDate,
                            ModifiedDate = fileInfo.ModifiedDate,
                            Type = PHOTO_EXTENSIONS.Contains(extension) ? MediaType.Photo : MediaType.Video
                        };

                        mediaFiles.Add(mediaFile);
                        fileCount++;
                        
                        // WHY: Report progress every 10 files (not every file - reduces UI updates)
                        if (fileCount % 10 == 0)
                        {
                            progress?.Report(fileCount);
                        }
                    }
                    catch
                    {
                        // WHY: One corrupted file shouldn't abort entire scan
                        continue;
                    }
                }
            }

            progress?.Report(fileCount); // Final count
            return mediaFiles;
        }, cancellationToken);
    }

    /// <summary>
    /// Start the AFC service for accessing iPhone filesystem.
    /// WHY: AFC is a privileged service - requires Lockdown authorization.
    /// </summary>
    private AfcClientHandle StartAFCService(string udid)
    {
        iDeviceHandle? deviceHandle = null;
        LockdownServiceDescriptorHandle? serviceDescriptor = null;

        try
        {
            // STEP 1: Get authenticated Lockdown client
            var lockdownClient = _deviceManager.GetLockdownClient(udid);

            // STEP 2: Request AFC service
            // WHY: Lockdown acts as service broker - validates pairing, then opens AFC port
            var lockdownError = LibiMobileDevice.Instance.Lockdown.lockdownd_start_service(
                lockdownClient,
                "com.apple.afc",  // WHY: AFC service identifier (defined by Apple)
                out serviceDescriptor
            );

            if (lockdownError != LockdownError.Success || serviceDescriptor == null || serviceDescriptor.IsInvalid)
            {
                throw new iPhoneException(
                    $"Failed to start AFC service: {lockdownError}. Please unlock your iPhone and ensure it is trusted.",
                    lockdownError == LockdownError.PasswordProtected 
                        ? iPhoneErrorType.DeviceLocked 
                        : iPhoneErrorType.ServiceUnavailable
                ) { DeviceUDID = udid };
            }

            // STEP 3: Create AFC client using the service port
            // WHY: AFC client handles the actual file protocol (list, read, stat, etc.)
            var ideviceError = LibiMobileDevice.Instance.iDevice.idevice_new(out deviceHandle, udid);
            if (ideviceError != iDeviceError.Success || deviceHandle == null || deviceHandle.IsInvalid)
            {
                throw new iPhoneException(
                    "Failed to reconnect to device for AFC. Please reconnect your iPhone.",
                    iPhoneErrorType.DeviceNotFound
                ) { DeviceUDID = udid };
            }

            AfcClientHandle afcHandle;
            var afcError = LibiMobileDevice.Instance.Afc.afc_client_new(
                deviceHandle,
                serviceDescriptor,
                out afcHandle
            );

            if (afcError != AfcError.Success || afcHandle == null || afcHandle.IsInvalid)
            {
                throw new iPhoneException(
                    $"Failed to create AFC client: {afcError}",
                    iPhoneErrorType.ServiceUnavailable
                ) { DeviceUDID = udid };
            }

            return afcHandle;
        }
        catch (iPhoneException)
        {
            throw; // Re-throw our custom exceptions
        }
        catch (Exception ex)
        {
            throw new iPhoneException(
                "Failed to access iPhone filesystem. Please disconnect and reconnect your iPhone.",
                ex,
                iPhoneErrorType.ServiceUnavailable
            ) { DeviceUDID = udid };
        }
        finally
        {
            // WHY: Always clean up intermediate handles
            try { serviceDescriptor?.Dispose(); } catch { }
            try { deviceHandle?.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// List files/folders in a directory on iPhone.
    /// WHY: AFC protocol's directory listing command.
    /// </summary>
    private List<string> ListDirectory(AfcClientHandle afcHandle, string path)
    {
        var items = new List<string>();

        ReadOnlyCollection<string> directoryList;
        
        // WHY: Read directory contents
        var afcError = LibiMobileDevice.Instance.Afc.afc_read_directory(
            afcHandle,
            path,
            out directoryList
        );

        if (afcError != AfcError.Success)
        {
            // WHY: Empty folder or access denied - return empty list
            return items;
        }

        foreach (var item in directoryList)
        {
            // WHY: Skip . and .. (current/parent directory markers)
            if (item == "." || item == "..")
                continue;

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Get file metadata (size, dates) without downloading content.
    /// WHY: AFC stat command - returns file info structure.
    /// </summary>
    private (long Size, DateTime CreatedDate, DateTime ModifiedDate) GetFileInfo(AfcClientHandle afcHandle, string filePath)
    {
        ReadOnlyCollection<string> fileInfo;
        
        // WHY: Get file info (returns key-value pairs: st_size, st_mtime, etc.)
        var afcError = LibiMobileDevice.Instance.Afc.afc_get_file_info(
            afcHandle,
            filePath,
            out fileInfo
        );

        if (afcError != AfcError.Success)
        {
            throw new iPhoneException($"Failed to get file info for {filePath}", iPhoneErrorType.FileNotFound);
        }

        // WHY: Parse file info dictionary (odd indices = keys, even indices = values)
        var infoDict = new Dictionary<string, string>();
        for (int i = 0; i < fileInfo.Count - 1; i += 2)
        {
            infoDict[fileInfo[i]] = fileInfo[i + 1];
        }

        // WHY: st_size = file size in bytes
        long size = 0;
        if (infoDict.TryGetValue("st_size", out var sizeStr))
        {
            long.TryParse(sizeStr, out size);
        }

        // WHY: st_mtime = modification time (Unix timestamp in nanoseconds)
        DateTime modifiedDate = DateTime.Now;
        if (infoDict.TryGetValue("st_mtime", out var mtimeStr))
        {
            if (long.TryParse(mtimeStr, out var mtimeNanos))
            {
                // WHY: Convert nanoseconds to DateTime
                modifiedDate = DateTimeOffset.FromUnixTimeMilliseconds(mtimeNanos / 1_000_000).DateTime;
            }
        }

        // WHY: st_birthtime = creation time (iOS-specific, not in standard Unix)
        DateTime createdDate = modifiedDate; // Default to modified date
        if (infoDict.TryGetValue("st_birthtime", out var birthtimeStr))
        {
            if (long.TryParse(birthtimeStr, out var birthtimeNanos))
            {
                createdDate = DateTimeOffset.FromUnixTimeMilliseconds(birthtimeNanos / 1_000_000).DateTime;
            }
        }

        return (size, createdDate, modifiedDate);
    }

    /// <summary>
    /// Load a thumbnail for a media file.
    /// WHY: Download small portion of file, decode HEIC, resize, encode as JPEG for WPF.
    /// </summary>
    public async Task LoadThumbnailAsync(string udid, MediaFile mediaFile, int maxSize = 200)
    {
        await Task.Run(() =>
        {
            using var afcHandle = StartAFCService(udid);

            try
            {
                // WHY: Photos have embedded thumbnails OR we can read first few KB and resize
                // Videos: Read a frame (complex) OR show generic video icon
                if (mediaFile.Type == MediaType.Video)
                {
                    // WHY: Video thumbnail generation requires FFmpeg - out of scope for now
                    // Set to null - UI will show video icon instead
                    mediaFile.ThumbnailData = null;
                    return;
                }

                // PHOTO THUMBNAIL STRATEGY:
                // Option 1: Read EXIF thumbnail (fast, already small)
                // Option 2: Download first 50KB, decode, resize (works for all formats)
                // WHY: We use Option 2 (simpler, works for HEIC without EXIF reader)

                // WHY: Open file in read mode
                ulong fileHandle = 0;
                var afcError = LibiMobileDevice.Instance.Afc.afc_file_open(
                    afcHandle,
                    mediaFile.FilePath,
                    AfcFileMode.FopenRdonly,
                    ref fileHandle
                );

                if (afcError != AfcError.Success)
                {
                    mediaFile.ThumbnailData = null;
                    return;
                }

                try
                {
                    // WHY: Read first 100KB (enough for thumbnail in most JPG/HEIC)
                    // HEIC stores thumbnail at file start
                    const int chunkSize = 100 * 1024;
                    byte[] buffer = new byte[chunkSize];
                    uint bytesRead = 0;

                    afcError = LibiMobileDevice.Instance.Afc.afc_file_read(
                        afcHandle,
                        fileHandle,
                        buffer,
                        (uint)chunkSize,
                        ref bytesRead
                    );

                    if (afcError != AfcError.Success || bytesRead == 0)
                    {
                        mediaFile.ThumbnailData = null;
                        return;
                    }

                    // WHY: Resize to maxSize x maxSize using ImageSharp
                    using var image = Image.Load(new ReadOnlySpan<byte>(buffer, 0, (int)bytesRead));
                    
                    // WHY: Maintain aspect ratio
                    var ratio = Math.Min((double)maxSize / image.Width, (double)maxSize / image.Height);
                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    image.Mutate(x => x.Resize(newWidth, newHeight));

                    // WHY: Encode as JPEG (WPF-friendly, smaller than PNG)
                    using var ms = new MemoryStream();
                    image.Save(ms, new JpegEncoder { Quality = 75 });
                    mediaFile.ThumbnailData = ms.ToArray();
                }
                finally
                {
                    // WHY: Always close file handle (prevent resource leak on iPhone)
                    LibiMobileDevice.Instance.Afc.afc_file_close(afcHandle, fileHandle);
                }
            }
            catch
            {
                // WHY: Thumbnail generation is optional - don't fail entire operation
                mediaFile.ThumbnailData = null;
            }
        });
    }

    /// <summary>
    /// Detect Live Photo pairs (HEIC + MOV with same base name).
    /// WHY: Live Photos must be transferred together to maintain association.
    /// </summary>
    public async Task<List<MediaFile>> DetectLivePhotosAsync(List<MediaFile> mediaFiles)
    {
        return await Task.Run(() =>
        {
            // WHY: Create lookup by base name (IMG_0001 without extension)
            var filesByBaseName = mediaFiles
                .GroupBy(f => Path.GetFileNameWithoutExtension(f.FileName))
                .Where(g => g.Count() > 1) // Live Photos have at least 2 files
                .ToList();

            foreach (var group in filesByBaseName)
            {
                var heicFile = group.FirstOrDefault(f => 
                    Path.GetExtension(f.FileName).Equals(".heic", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(f.FileName).Equals(".heif", StringComparison.OrdinalIgnoreCase)
                );

                var movFile = group.FirstOrDefault(f => 
                    Path.GetExtension(f.FileName).Equals(".mov", StringComparison.OrdinalIgnoreCase)
                );

                // WHY: Valid Live Photo = HEIC + MOV with matching names
                if (heicFile != null && movFile != null)
                {
                    heicFile.LivePhotoCompanionPath = movFile.FilePath;
                    movFile.LivePhotoCompanionPath = heicFile.FilePath;
                }
            }

            return mediaFiles;
        });
    }

    public async Task<List<MediaFile>> GetMediaFilesSinceDateAsync(string udid, DateTime sinceDate)
    {
        var allFiles = await GetAllMediaFilesAsync(udid);
        
        // WHY: Filter by modified date (photos edited on iPhone get new modified date)
        return allFiles
            .Where(f => f.ModifiedDate > sinceDate)
            .ToList();
    }

    public async Task<int> GetMediaFileCountAsync(string udid)
    {
        return await Task.Run(() =>
        {
            using var afcHandle = StartAFCService(udid);
            
            var count = 0;
            var dcimFolders = ListDirectory(afcHandle, DCIM_PATH);

            foreach (var folder in dcimFolders)
            {
                if (folder.StartsWith("."))
                    continue;

                var folderPath = $"{DCIM_PATH}/{folder}";
                var files = ListDirectory(afcHandle, folderPath);

                count += files.Count(file =>
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    return PHOTO_EXTENSIONS.Contains(ext) || VIDEO_EXTENSIONS.Contains(ext);
                });
            }

            return count;
        });
    }
}
