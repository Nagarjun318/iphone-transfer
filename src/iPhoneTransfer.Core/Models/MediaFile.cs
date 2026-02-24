namespace iPhoneTransfer.Core.Models;

/// <summary>
/// Represents a photo or video file on the iPhone.
/// WHY: Encapsulates all metadata needed for display and transfer without loading the actual file.
/// </summary>
public class MediaFile
{
    /// <summary>
    /// Full path on iPhone (e.g., "/DCIM/100APPLE/IMG_0001.HEIC")
    /// WHY: Needed for AFC file read operations
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "IMG_0001.HEIC")
    /// WHY: User-friendly display, also used for default destination filename
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// WHY: Display to user, calculate transfer time, allocate buffer size
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// When the photo/video was created (from file system metadata)
    /// WHY: Display in UI, sort by date, organize transferred files
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// When the file was last modified
    /// WHY: Detect if photo was edited on iPhone, preserve timestamps on Windows
    /// </summary>
    public DateTime ModifiedDate { get; set; }

    /// <summary>
    /// Media type based on file extension
    /// WHY: Filter photos vs videos, show different icons
    /// </summary>
    public MediaType Type { get; set; }

    /// <summary>
    /// Thumbnail image data (JPEG format, max 200x200)
    /// WHY: Display in grid without transferring full file (saves time and memory)
    /// NULL until explicitly loaded via LoadThumbnailAsync()
    /// </summary>
    public byte[]? ThumbnailData { get; set; }

    /// <summary>
    /// Companion file for Live Photos (e.g., IMG_0001.MOV paired with IMG_0001.HEIC)
    /// WHY: Live Photos consist of HEIC (still) + MOV (video) - must transfer both
    /// </summary>
    public string? LivePhotoCompanionPath { get; set; }

    /// <summary>
    /// Human-readable file size (e.g., "2.5 MB")
    /// WHY: Display in UI without formatting logic in XAML
    /// </summary>
    public string FormattedSize => FormatFileSize(FileSize);

    /// <summary>
    /// File extension in uppercase (e.g., "HEIC", "MOV", "JPG")
    /// WHY: Quick media type detection, display in UI
    /// </summary>
    public string Extension => Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant();

    /// <summary>
    /// Whether this file is selected for transfer
    /// WHY: Track user selection in UI (bound to checkbox)
    /// </summary>
    public bool IsSelected { get; set; }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Media file type classification.
/// WHY: Different handling for photos (can show thumbnail) vs videos (show play icon).
/// </summary>
public enum MediaType
{
    /// <summary>Unknown or unsupported file type</summary>
    Unknown,
    
    /// <summary>
    /// Still image: HEIC, HEIF, JPG, JPEG, PNG
    /// WHY: HEIC is Apple's default (High Efficiency Image Container)
    /// </summary>
    Photo,
    
    /// <summary>
    /// Video: MOV, MP4, M4V
    /// WHY: MOV is Apple's default (QuickTime container with HEVC codec)
    /// </summary>
    Video
}
