namespace iPhoneTransfer.Core.Exceptions;

/// <summary>
/// Base exception for all iPhone-related errors.
/// WHY: Catch-all for device communication failures, easier error handling in UI.
/// </summary>
public class iPhoneException : Exception
{
    /// <summary>
    /// Error category for specialized handling.
    /// WHY: Different UI responses (reconnect vs. unlock vs. trust).
    /// </summary>
    public iPhoneErrorType ErrorType { get; set; }

    /// <summary>
    /// Device UDID related to this error (if applicable).
    /// WHY: Multi-device support - know which iPhone failed.
    /// </summary>
    public string? DeviceUDID { get; set; }

    public iPhoneException(string message, iPhoneErrorType errorType = iPhoneErrorType.Unknown) 
        : base(message)
    {
        ErrorType = errorType;
    }

    public iPhoneException(string message, Exception innerException, iPhoneErrorType errorType = iPhoneErrorType.Unknown) 
        : base(message, innerException)
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Get user-friendly error message with recovery suggestions.
    /// WHY: Non-technical users need actionable guidance.
    /// </summary>
    public string GetUserFriendlyMessage()
    {
        return ErrorType switch
        {
            iPhoneErrorType.DeviceNotFound => 
                "No iPhone detected. Please connect your iPhone via USB cable and unlock it.",
            
            iPhoneErrorType.DeviceDisconnected => 
                "iPhone was disconnected during the operation. Please reconnect and try again.",
            
            iPhoneErrorType.DeviceLocked => 
                "Your iPhone is locked. Please unlock your iPhone and try again.",
            
            iPhoneErrorType.NotPaired => 
                "This computer is not trusted. Please tap 'Trust' on your iPhone when prompted.",
            
            iPhoneErrorType.PairingFailed => 
                "Failed to pair with your iPhone. Make sure you tap 'Trust' and enter your passcode.",
            
            iPhoneErrorType.ServiceUnavailable => 
                "Could not access the Photos service on your iPhone. Try disconnecting and reconnecting.",
            
            iPhoneErrorType.FileNotFound => 
                $"File not found on iPhone: {Message}",
            
            iPhoneErrorType.TransferFailed => 
                $"Failed to transfer file: {Message}. Check available disk space and try again.",
            
            iPhoneErrorType.UsbmuxdNotFound => 
                "Apple Mobile Device Support is not installed. Please install iTunes or Apple Devices app.",
            
            iPhoneErrorType.PermissionDenied => 
                "Permission denied. Make sure your iPhone is unlocked and trusted.",
            
            iPhoneErrorType.InvalidPairingRecord => 
                "The trust relationship with your iPhone is invalid. Please disconnect, forget this computer on your iPhone, and re-pair.",
            
            iPhoneErrorType.UnsupportediOSVersion => 
                $"This iOS version may not be fully supported: {Message}",
            
            _ => Message
        };
    }

    /// <summary>
    /// Whether this error is recoverable (user can fix) or fatal (app limitation).
    /// WHY: Decide whether to show "Retry" button or "Close" button.
    /// </summary>
    public bool IsRecoverable => ErrorType switch
    {
        iPhoneErrorType.DeviceNotFound => true,      // User can connect device
        iPhoneErrorType.DeviceDisconnected => true,  // User can reconnect
        iPhoneErrorType.DeviceLocked => true,        // User can unlock
        iPhoneErrorType.NotPaired => true,           // User can trust
        iPhoneErrorType.PairingFailed => true,       // User can retry trust
        iPhoneErrorType.UsbmuxdNotFound => false,    // Requires iTunes installation
        iPhoneErrorType.UnsupportediOSVersion => false, // App update needed
        _ => true
    };
}

/// <summary>
/// Categories of iPhone-related errors.
/// WHY: Enable specific error handling logic without string parsing.
/// </summary>
public enum iPhoneErrorType
{
    Unknown,
    
    /// <summary>No iPhone connected to USB</summary>
    DeviceNotFound,
    
    /// <summary>iPhone was unplugged during operation</summary>
    DeviceDisconnected,
    
    /// <summary>iPhone is locked (passcode screen)</summary>
    DeviceLocked,
    
    /// <summary>PC is not trusted ("Trust This Computer" not completed)</summary>
    NotPaired,
    
    /// <summary>Pairing attempt failed (user tapped "Don't Trust" or no passcode entered)</summary>
    PairingFailed,
    
    /// <summary>Pairing record exists but is invalid (iPhone was reset)</summary>
    InvalidPairingRecord,
    
    /// <summary>AFC or other service could not be started</summary>
    ServiceUnavailable,
    
    /// <summary>File path exists in metadata but not on device (rare, but possible)</summary>
    FileNotFound,
    
    /// <summary>File copy failed (disk full, corrupted data, network error for Wi-Fi)</summary>
    TransferFailed,
    
    /// <summary>usbmuxd.exe not found (iTunes not installed)</summary>
    UsbmuxdNotFound,
    
    /// <summary>iOS denied access (sandboxing, security policy)</summary>
    PermissionDenied,
    
    /// <summary>iOS version is too new or too old for this app</summary>
    UnsupportediOSVersion,
    
    /// <summary>Timeout waiting for device or service response</summary>
    Timeout,
    
    /// <summary>Network error (only for Wi-Fi sync mode)</summary>
    NetworkError
}
