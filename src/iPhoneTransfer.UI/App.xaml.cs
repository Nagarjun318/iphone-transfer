using System.Windows;
using iPhoneTransfer.Services;
using iPhoneTransfer.UI.ViewModels;
using iPhoneTransfer.UI.Views;

// WHY: Disambiguate WPF Application from WinForms Application (both in scope due to UseWindowsForms)
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace iPhoneTransfer.UI;

/// <summary>
/// Application entry point and dependency injection setup.
/// WHY: Configure services before showing UI.
/// </summary>
public partial class App : Application
{
    private DeviceManager? _deviceManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // WHY: Initialize DeviceManager early to detect iTunes installation issues
            _deviceManager = new DeviceManager();

            // WHY: Create services with dependency injection
            var photoService = new PhotoLibraryService(_deviceManager);
            var transferService = new FileTransferService(_deviceManager);

            // WHY: Create main view model with services
            var mainViewModel = new MainViewModel(
                _deviceManager,
                photoService,
                transferService
            );

            // WHY: Create and show main window
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // WHY: Set as application MainWindow so app shuts down when it closes
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // WHY: Show error if iTunes/Apple Mobile Device Support not installed
            MessageBox.Show(
                $"Failed to initialize application:\n\n{ex.Message}\n\n" +
                "Please install iTunes or Apple Devices app to use this application.",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // WHY: Clean up device connections
        _deviceManager?.Dispose();
        base.OnExit(e);
    }
}
