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

        // WHY: Catch unhandled exceptions to prevent silent crashes and log errors
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] UNHANDLED: {ex}\n\n");
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{ex.Message}\n\nDetails have been saved to error.log",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] UI ERROR: {args.Exception}\n\n");

            // WHY: Show user-friendly message for known iPhone errors
            var message = args.Exception is iPhoneTransfer.Core.Exceptions.iPhoneException iphoneEx
                ? iphoneEx.GetUserFriendlyMessage()
                : $"An error occurred: {args.Exception.Message}";

            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // WHY: Prevent app crash, allow user to continue
        };

        // WHY: Catch unobserved Task exceptions (background threads)
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] TASK ERROR: {args.Exception}\n\n");
            args.SetObserved(); // WHY: Prevent app crash from background task failures
        };

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
