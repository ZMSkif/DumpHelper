using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupFinder.App.Resources;
using DupFinder.App.Services;
using DupFinder.Core.Model;

namespace DupFinder.App.ViewModels;

/// <summary>
/// Связывает вкладки: запускает поиск, отдаёт пачки результатов в таблицу
/// и следит за отменой. Сама работа здесь не делается — только координация.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ScanRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly Dispatcher _dispatcher;

    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private int _selectedTab;

    public MainViewModel(
        ScanViewModel scan,
        ResultsViewModel results,
        ScanRunner runner,
        IDialogService dialogs,
        IShellService shell,
        Dispatcher dispatcher)
    {
        Scan = scan;
        Results = results;
        _runner = runner;
        _dialogs = dialogs;
        _shell = shell;
        _dispatcher = dispatcher;
    }

    public ScanViewModel Scan { get; }

    public ResultsViewModel Results { get; }

    /// <summary>Идёт ли сейчас поиск.</summary>
    public bool IsScanning => Scan.IsBusy;

    /// <summary>Показывает журнал работы прямо в приложении.</summary>
    [RelayCommand]
    private void OpenLog() => _dialogs.ShowLog();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartScanAsync()
    {
        var options = Scan.BuildOptions();
        if (options is null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        Scan.BeginScan();
        Results.Clear();
        SelectedTab = 1;
        NotifyScanState();

        var progress = new Progress<ScanProgress>(Scan.ApplyProgress);

        try
        {
            var summary = await _runner.RunAsync(options, progress, PublishAsync, _cts.Token).ConfigureAwait(true);
            Results.ApplySummary(summary);
            Scan.EndScan(string.Format(
                Strings.DoneFormat,
                Scan.Elapsed.ToString(@"m\:ss"),
                summary?.Groups ?? 0));

            if (!Results.HasRows)
            {
                Results.StatisticsText = Strings.NothingFound;
            }
        }
        catch (OperationCanceledException)
        {
            Scan.EndScan(Strings.StageCancelled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            Serilog.Log.Error(ex, "Поиск завершился ошибкой");
            Scan.EndScan(string.Format(Strings.ErrorScanFormat, ex.Message));
            _dialogs.ShowError(string.Format(Strings.ErrorScanFormat, ex.Message), AppPaths.Logs);
        }
        finally
        {
            if (Scan.IsBusy)
            {
                // Сюда попадаем, если вылетело что-то, чего мы не предусмотрели.
                // Оставить кнопку «Начать» заблокированной навсегда — хуже любой ошибки.
                Scan.EndScan(Strings.StageCancelled);
            }

            _cts?.Dispose();
            _cts = null;
            NotifyScanState();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelScan()
    {
        _cts?.Cancel();
        Scan.StatusText = Strings.StageCancelled;
    }

    private bool CanStart() => !Scan.IsBusy;

    private bool CanCancel() => Scan.IsBusy;

    /// <summary>
    /// Отдаёт пачку групп в таблицу. Вызывается из фонового потока,
    /// поэтому в UI попадаем через Dispatcher — и только один раз на пачку.
    /// </summary>
    private async Task PublishAsync(IReadOnlyList<DuplicateGroup> groups) =>
        await _dispatcher.InvokeAsync(() => Results.AddGroups(groups), DispatcherPriority.Background);

    private void NotifyScanState()
    {
        OnPropertyChanged(nameof(IsScanning));
        StartScanCommand.NotifyCanExecuteChanged();
        CancelScanCommand.NotifyCanExecuteChanged();
    }
}
