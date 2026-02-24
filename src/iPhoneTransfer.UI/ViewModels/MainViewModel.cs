using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iPhoneTransfer.Core.Exceptions;
using iPhoneTransfer.Core.Interfaces;
using iPhoneTransfer.Core.Models;
using iPhoneTransfer.Services;
using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;

namespace iPhoneTransfer.UI.ViewModels;

/// <summary>
/// Main view model for the application.
/// WHY: MVVM pattern separates UI logic from business logic, enables data binding.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DeviceManager _deviceManager;
    private readonly IPhotoService _photoService;
    private readonly ITransferService _transferService;

    private CancellationTokenSource? _cancellationTokenSource;

    #region Observable Properties
    
    // WHY: ObservableProperty generates INotifyPropertyChanged implementation
    
    [ObservableProperty]
    private DeviceInfo? _connectedDevice;

    [ObservableProperty]
    private string _statusMessage = "No iPhone connected. Please connect your iPhone and unlock it.";

    [ObservableProperty]
    private bool _isDeviceConnected;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isTransferring;

    [ObservableProperty]
    private string _scanProgress = "";

    [ObservableProperty]
    private TransferProgress? _transferProgress;

    [ObservableProperty]
    private string _selectedFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    #endregion

    /// <summary>
    /// WHY: ObservableCollection automatically notifies UI of changes (add/remove items)
    /// </summary>
    public ObservableCollection<MediaFile> MediaFiles { get; } = new();

    public MainViewModel(
        DeviceManager deviceManager,
        IPhotoService photoService,
        ITransferService transferService)
    {
        _deviceManager = deviceManager;
        _photoService = photoService;
        _transferService = transferService;

        // WHY: Subscribe to device connection events
        _deviceManager.DeviceConnected += OnDeviceConnected;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        // WHY: Check for already-connected device on startup
        Task.Run(async () =>
        {
            var devices = await _deviceManager.GetConnectedDevicesAsync();
            if (devices.Count > 0)
            {
                await HandleDeviceConnection(devices[0]);
            }
        });
    }

    private async void OnDeviceConnected(object? sender, DeviceInfo device)
    {
        // WHY: Device events fire on background thread - marshal to UI thread
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await HandleDeviceConnection(device);
        });
    }

    private async void OnDeviceDisconnected(object? sender, string udid)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (ConnectedDevice?.UDID == udid)
            {
                IsDeviceConnected = false;
                ConnectedDevice = null;
                StatusMessage = "iPhone disconnected. Please reconnect.";
                MediaFiles.Clear();

                // WHY: Cancel any ongoing operations
                _cancellationTokenSource?.Cancel();
            }
        });
    }

    private async Task HandleDeviceConnection(DeviceInfo device)
    {
        try
        {
            ConnectedDevice = device;

            // WHY: Check if device is paired
            if (!device.IsPaired)
            {
                StatusMessage = "Please tap 'Trust' on your iPhone...";
                
                // WHY: Initiate pairing
                var paired = await _deviceManager.PairDeviceAsync(device.UDID);
                if (!paired)
                {
                    StatusMessage = "Pairing failed. Please try again.";
                    return;
                }

                device.IsPaired = true;
            }

            // WHY: Check if device is locked
            if (device.IsLocked)
            {
                StatusMessage = "Please unlock your iPhone to continue.";
                IsDeviceConnected = false;
                return;
            }

            IsDeviceConnected = true;
            StatusMessage = $"Connected to {device.DisplayName}";

            // WHY: Auto-start photo scan on successful connection
            await ScanPhotosAsync();
        }
        catch (iPhoneException ex)
        {
            StatusMessage = ex.GetUserFriendlyMessage();
            IsDeviceConnected = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsDeviceConnected = false;
        }
    }

    /// <summary>
    /// Scan iPhone for photos and videos.
    /// WHY: RelayCommand enables button binding with ICommand.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScanPhotos))]
    private async Task ScanPhotosAsync()
    {
        if (ConnectedDevice == null) return;

        try
        {
            IsScanning = true;
            MediaFiles.Clear();
            StatusMessage = "Scanning for photos and videos...";

            _cancellationTokenSource = new CancellationTokenSource();

            // WHY: Progress callback updates UI during scan
            var progress = new Progress<int>(count =>
            {
                ScanProgress = $"Found {count} files...";
            });

            var files = await _photoService.GetAllMediaFilesAsync(
                ConnectedDevice.UDID,
                progress,
                _cancellationTokenSource.Token
            );

            // WHY: Detect Live Photo pairs
            files = await _photoService.DetectLivePhotosAsync(files);

            // WHY: Add to ObservableCollection on UI thread
            foreach (var file in files)
            {
                MediaFiles.Add(file);
            }

            StatusMessage = $"Found {MediaFiles.Count} photos and videos";
            
            // WHY: Load thumbnails in background (lazy loading)
            _ = LoadThumbnailsAsync();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled";
        }
        catch (iPhoneException ex)
        {
            StatusMessage = ex.GetUserFriendlyMessage();
            System.Windows.MessageBox.Show(ex.GetUserFriendlyMessage(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
            ScanProgress = "";
        }
    }

    private bool CanScanPhotos() => IsDeviceConnected && !IsScanning && !IsTransferring;

    /// <summary>
    /// Load thumbnails for visible photos (lazy loading).
    /// WHY: Loading all thumbnails upfront is too slow for thousands of photos.
    /// </summary>
    private async Task LoadThumbnailsAsync()
    {
        if (ConnectedDevice == null) return;

        // WHY: Load first 50 thumbnails immediately (visible in initial scroll)
        var filesToLoad = MediaFiles.Where(f => f.Type == MediaType.Photo).Take(50).ToList();

        foreach (var file in filesToLoad)
        {
            try
            {
                await _photoService.LoadThumbnailAsync(ConnectedDevice.UDID, file);
                
                // WHY: Trigger UI update for this specific item
                // Note: ObservableCollection doesn't auto-detect property changes on items
                // TODO: Implement INotifyPropertyChanged on MediaFile for automatic updates
            }
            catch
            {
                // WHY: Thumbnail loading is optional - skip failures
            }
        }
    }

    /// <summary>
    /// Select destination folder for transferred files.
    /// </summary>
    [RelayCommand]
    private void SelectFolder()
    {
        // WHY: OpenFolderDialog is WPF-native (.NET 6+), no WinForms dependency needed
        var dialog = new OpenFolderDialog
        {
            Title = "Select destination folder for photos and videos",
            InitialDirectory = SelectedFolder
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolder = dialog.FolderName;
        }
    }

    /// <summary>
    /// Select all media files.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in MediaFiles)
        {
            file.IsSelected = true;
        }
    }

    /// <summary>
    /// Deselect all media files.
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var file in MediaFiles)
        {
            file.IsSelected = false;
        }
    }

    /// <summary>
    /// Transfer selected files to Windows.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartTransfer))]
    private async Task StartTransferAsync()
    {
        if (ConnectedDevice == null) return;

        var selectedFiles = MediaFiles.Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            System.Windows.MessageBox.Show("Please select files to transfer", "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            IsTransferring = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // WHY: Progress callback updates UI during transfer
            var progress = new Progress<TransferProgress>(p =>
            {
                TransferProgress = p;
                StatusMessage = p.StatusText;
            });

            var successCount = await _transferService.TransferFilesAsync(
                ConnectedDevice.UDID,
                selectedFiles,
                SelectedFolder,
                progress,
                _cancellationTokenSource.Token
            );

            if (successCount == selectedFiles.Count)
            {
                System.Windows.MessageBox.Show(
                    $"Successfully transferred {successCount} file(s) to:\n{SelectedFolder}",
                    "Transfer Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Transferred {successCount} of {selectedFiles.Count} file(s).\n\nSome files failed to transfer.",
                    "Transfer Completed with Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Transfer cancelled";
        }
        catch (iPhoneException ex)
        {
            System.Windows.MessageBox.Show(ex.GetUserFriendlyMessage(), "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsTransferring = false;
            TransferProgress = null;
        }
    }

    private bool CanStartTransfer() => IsDeviceConnected && !IsScanning && !IsTransferring && MediaFiles.Any(f => f.IsSelected);

    /// <summary>
    /// Cancel ongoing operation (scan or transfer).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    private bool CanCancel() => IsScanning || IsTransferring;
}
