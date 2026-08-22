using System.Windows;
using System.Windows.Threading;
using DupFinder.App.Services;
using DupFinder.App.ViewModels;
using DupFinder.Core.Files;
using DupFinder.Core.Scanning;
using Serilog;
using Strings = DupFinder.App.Resources.Strings;

namespace DupFinder.App;

/// <summary>
/// Точка входа. Состав объектов собирается здесь вручную: контейнер
/// на этом объёме ничего не даёт, а стартовать надо быстро (ТЗ §3 — окно за 1,5 с).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPaths.EnsureCreated();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.Logs, "dupfinder-.log"),
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Необработанная ошибка");

        Log.Information("DupFinder Pro запускается: {Build}", BuildInfo.FullLabel);

        var shell = new ShellService();
        var dialogs = new DialogService(shell);
        var scanLog = new SerilogScanLog(Log.Logger);
        var scanner = DuplicateScanner.CreateDefault(new FileSystemFileSource(scanLog), scanLog);

        var main = new MainViewModel(
            new ScanViewModel(dialogs),
            new ResultsViewModel(dialogs, shell, new ShellRecycleBin()),
            new ScanRunner(scanner),
            dialogs,
            shell,
            Dispatcher.CurrentDispatcher);

        MainWindow = new MainWindow { DataContext = main };
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("DupFinder Pro завершает работу");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Необработанная ошибка в интерфейсе");
        MessageBox.Show(
            $"{e.Exception.Message}\n\n{Strings.ErrorOpenLogPrompt}",
            Strings.AppTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
