namespace iPhoneTransfer.Core.Models;

/// <summary>
/// Tracks the progress of a file transfer operation.
/// WHY: Provides real-time feedback to UI, allows cancellation, enables resume on failure.
/// </summary>
public class TransferProgress
{
    /// <summary>
    /// The file currently being transferred
    /// WHY: Display in progress UI "Copying IMG_0001.HEIC..."
    /// </summary>
    public MediaFile? CurrentFile { get; set; }

    /// <summary>
    /// Number of bytes transferred for the current file
    /// WHY: Calculate percentage for progress bar
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Total bytes for the current file
    /// WHY: Calculate percentage = BytesTransferred / TotalBytes * 100
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Percentage complete for current file (0-100)
    /// WHY: Bind directly to ProgressBar.Value
    /// </summary>
    public double PercentComplete => TotalBytes > 0 
        ? Math.Round((double)BytesTransferred / TotalBytes * 100, 1) 
        : 0;

    /// <summary>
    /// Number of files completed in this batch
    /// WHY: Display "Copied 5 of 20 files"
    /// </summary>
    public int FilesCompleted { get; set; }

    /// <summary>
    /// Total number of files to transfer
    /// WHY: Show overall progress
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Overall percentage across all files (0-100)
    /// WHY: Show completion of entire batch
    /// </summary>
    public double OverallPercent => TotalFiles > 0 
        ? Math.Round((double)FilesCompleted / TotalFiles * 100, 1) 
        : 0;

    /// <summary>
    /// When the transfer started
    /// WHY: Calculate elapsed time, estimated time remaining
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Current transfer speed in bytes per second
    /// WHY: Display "15 MB/s" to user, estimate time remaining
    /// </summary>
    public long BytesPerSecond { get; set; }

    /// <summary>
    /// Estimated time remaining for all transfers
    /// WHY: User experience - "About 2 minutes remaining"
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (BytesPerSecond == 0) return null;
            
            var totalBytesToTransfer = TotalFiles > 0 
                ? (long)(TotalBytes * TotalFiles) 
                : TotalBytes;
            var totalBytesTransferred = (long)(TotalBytes * FilesCompleted) + BytesTransferred;
            var remainingBytes = totalBytesToTransfer - totalBytesTransferred;
            
            return TimeSpan.FromSeconds(remainingBytes / (double)BytesPerSecond);
        }
    }

    /// <summary>
    /// Current operation status
    /// WHY: Show different UI states (transferring, paused, completed, error)
    /// </summary>
    public TransferStatus Status { get; set; } = TransferStatus.Idle;

    /// <summary>
    /// Error message if Status == Error
    /// WHY: Display to user, log for debugging
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of files that failed to transfer
    /// WHY: Allow user to retry failed files
    /// </summary>
    public List<MediaFile> FailedFiles { get; set; } = new();

    /// <summary>
    /// Whether the transfer can be cancelled
    /// WHY: Enable/disable Cancel button in UI
    /// </summary>
    public bool CanCancel => Status == TransferStatus.Transferring;

    /// <summary>
    /// User-friendly status text for display
    /// Example: "Transferring IMG_0001.HEIC (5 of 20) - 15 MB/s"
    /// </summary>
    public string StatusText
    {
        get
        {
            return Status switch
            {
                TransferStatus.Idle => "Ready to transfer",
                TransferStatus.Transferring when CurrentFile != null => 
                    $"Copying {CurrentFile.FileName} ({FilesCompleted + 1} of {TotalFiles}) - {FormatSpeed(BytesPerSecond)}",
                TransferStatus.Paused => "Transfer paused",
                TransferStatus.Completed => $"Completed {FilesCompleted} file(s)",
                TransferStatus.Error => $"Error: {ErrorMessage}",
                TransferStatus.Cancelled => "Transfer cancelled",
                _ => "Unknown status"
            };
        }
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
        return $"{bytesPerSecond / (1024.0 * 1024):F1} MB/s";
    }

    /// <summary>
    /// Update transfer speed based on bytes transferred since last update.
    /// WHY: Smooth speed calculation (exponential moving average to avoid spikes).
    /// </summary>
    public void UpdateSpeed(long bytesSinceLastUpdate, TimeSpan timeSinceLastUpdate)
    {
        if (timeSinceLastUpdate.TotalSeconds > 0)
        {
            var instantSpeed = (long)(bytesSinceLastUpdate / timeSinceLastUpdate.TotalSeconds);
            // Exponential moving average: 70% new speed + 30% old speed
            BytesPerSecond = (long)(instantSpeed * 0.7 + BytesPerSecond * 0.3);
        }
    }
}

/// <summary>
/// Transfer operation states.
/// WHY: Controls UI state machine (buttons, progress visibility, error display).
/// </summary>
public enum TransferStatus
{
    /// <summary>No transfer in progress</summary>
    Idle,
    
    /// <summary>
    /// Currently transferring files
    /// WHY: Show progress bar, disable Start button, enable Cancel button
    /// </summary>
    Transferring,
    
    /// <summary>
    /// Transfer paused by user
    /// WHY: Allow resume without restarting
    /// </summary>
    Paused,
    
    /// <summary>
    /// All files transferred successfully
    /// WHY: Show success message, re-enable file selection
    /// </summary>
    Completed,
    
    /// <summary>
    /// Transfer failed
    /// WHY: Display error, offer retry
    /// </summary>
    Error,
    
    /// <summary>
    /// Transfer cancelled by user
    /// WHY: Clean up partial files, reset UI
    /// </summary>
    Cancelled
}
