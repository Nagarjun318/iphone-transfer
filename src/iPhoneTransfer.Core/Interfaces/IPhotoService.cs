using iPhoneTransfer.Core.Models;

namespace iPhoneTransfer.Core.Interfaces;

/// <summary>
/// Service for browsing and enumerating photos/videos on an iPhone.
/// WHY: Abstracts AFC protocol, provides high-level media library access.
/// </summary>
public interface IPhotoService
{
    /// <summary>
    /// Get all photos and videos from the iPhone's camera roll.
    /// WHY: Primary function - enumerate DCIM folder for display.
    /// </summary>
    /// <param name="udid">Device to scan</param>
    /// <param name="progress">Optional progress callback (reports file count)</param>
    /// <param name="cancellationToken">Allow user to cancel long scans</param>
    /// <returns>List of media files with metadata (no thumbnails or file data)</returns>
    Task<List<MediaFile>> GetAllMediaFilesAsync(
        string udid, 
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load thumbnail image data for a media file.
    /// WHY: Separate from GetAllMediaFilesAsync to avoid loading thousands of thumbnails upfront.
    /// Lazy-load thumbnails as user scrolls.
    /// </summary>
    /// <param name="udid">Device containing the file</param>
    /// <param name="mediaFile">File to get thumbnail for (updates mediaFile.ThumbnailData)</param>
    /// <param name="maxSize">Maximum thumbnail dimension (e.g., 200 for 200x200)</param>
    Task LoadThumbnailAsync(string udid, MediaFile mediaFile, int maxSize = 200);

    /// <summary>
    /// Detect Live Photo pairs (HEIC + MOV with same base name).
    /// WHY: Live Photos are stored as two separate files, must transfer both.
    /// Example: IMG_0001.HEIC + IMG_0001.MOV = one Live Photo.
    /// </summary>
    /// <param name="mediaFiles">List to scan for pairs</param>
    /// <returns>Updated list with LivePhotoCompanionPath populated</returns>
    Task<List<MediaFile>> DetectLivePhotosAsync(List<MediaFile> mediaFiles);

    /// <summary>
    /// Get files modified after a specific date.
    /// WHY: Incremental sync - only transfer new photos since last backup.
    /// </summary>
    /// <param name="udid">Device to scan</param>
    /// <param name="sinceDate">Only return files modified after this date</param>
    Task<List<MediaFile>> GetMediaFilesSinceDateAsync(string udid, DateTime sinceDate);

    /// <summary>
    /// Get total count of media files without loading metadata.
    /// WHY: Quick preview "Your iPhone has 1,234 photos" before full scan.
    /// </summary>
    /// <param name="udid">Device to count</param>
    Task<int> GetMediaFileCountAsync(string udid);
}
