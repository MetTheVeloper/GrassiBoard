using System.Windows;
using System.Windows.Threading;
using GrassiBoard.Services;

namespace GrassiBoard;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
        try
        {
            ThemeManager.Initialize();
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            ShowFatalError(exception, "Application startup");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowFatalError(e.Exception, "WPF dispatcher");
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception exception = e.ExceptionObject as Exception ??
            new InvalidOperationException($"Unhandled non-Exception object: {e.ExceptionObject}");
        CrashReporter.Report(exception, "AppDomain unhandled exception", e.IsTerminating);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashReporter.Report(e.Exception, "Unobserved background task", false);
        e.SetObserved();
    }

    private void ShowFatalError(Exception exception, string context)
    {
        if (!CrashReporter.BeginFatalReport())
        {
            Shutdown(1);
            return;
        }

        string logPath = CrashReporter.Report(exception, context, true);
        MessageBox.Show(
            $"GrassiBoard encountered an unexpected error and must close.\n\n" +
            $"{exception.GetType().Name}: {exception.Message}\n\nCrash report:\n{logPath}",
            "GrassiBoard crash report",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }
}
