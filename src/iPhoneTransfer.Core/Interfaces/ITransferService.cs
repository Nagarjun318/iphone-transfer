using iPhoneTransfer.Core.Models;

namespace iPhoneTransfer.Core.Interfaces;

/// <summary>
/// Service for transferring media files from iPhone to Windows.
/// WHY: Handles actual file copy with progress tracking, error recovery, cancellation.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Transfer multiple files from iPhone to a destination folder.
    /// WHY: Batch operation with overall progress - more efficient than individual transfers.
    /// </summary>
    /// <param name="udid">Source device</param>
    /// <param name="mediaFiles">Files to transfer</param>
    /// <param name="destinationFolder">Windows folder path (e.g., "C:\Users\John\Pictures\iPhone")</param>
    /// <param name="progress">Real-time progress updates (UI binding)</param>
    /// <param name="cancellationToken">Allow user to cancel mid-transfer</param>
    /// <returns>Number of files successfully transferred</returns>
    Task<int> TransferFilesAsync(
        string udid,
        List<MediaFile> mediaFiles,
        string destinationFolder,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfer a single file (used internally by TransferFilesAsync).
    /// WHY: Exposed for retry logic, advanced users who want one-by-one control.
    /// </summary>
    /// <param name="udid">Source device</param>
    /// <param name="mediaFile">File to transfer</param>
    /// <param name="destinationPath">Full Windows file path (including filename)</param>
    /// <param name="progress">Progress for this specific file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TransferFileAsync(
        string udid,
        MediaFile mediaFile,
        string destinationPath,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify file integrity after transfer (compare checksums).
    /// WHY: Ensure byte-for-byte accuracy, critical for irreplaceable photos.
    /// </summary>
    /// <param name="sourceUdid">iPhone device</param>
    /// <param name="sourceFile">Original file on iPhone</param>
    /// <param name="destinationPath">Copied file on Windows</param>
    /// <returns>True if files match exactly</returns>
    Task<bool> VerifyFileIntegrityAsync(string sourceUdid, MediaFile sourceFile, string destinationPath);

    /// <summary>
    /// Preserve original file timestamps on Windows.
    /// WHY: Maintain created/modified dates from iPhone for photo organization.
    /// </summary>
    /// <param name="windowsFilePath">File that was just transferred</param>
    /// <param name="originalFile">Source file with original timestamps</param>
    Task PreserveFileTimestampsAsync(string windowsFilePath, MediaFile originalFile);

    /// <summary>
    /// Estimate total transfer time based on file sizes and connection speed.
    /// WHY: Show "About 5 minutes" to user before starting transfer.
    /// </summary>
    /// <param name="mediaFiles">Files to estimate</param>
    /// <param name="connectionSpeed">Bytes per second (detect from connection type)</param>
    TimeSpan EstimateTransferTime(List<MediaFile> mediaFiles, long connectionSpeed);
}
