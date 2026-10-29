using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows.Input;
using Golem.Tablet.Core.Logging;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class LogsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;

    public LogsViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        ClearCommand = new RelayCommand(Clear);
        ExportCommand = new RelayCommand(Export);

        ((INotifyCollectionChanged)Entries).CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(HasEntries));
        };
    }

    public ObservableCollection<LogEntry> Entries => _services.Log.Entries;

    public int EntryCount => Entries.Count;

    public bool HasEntries => Entries.Count > 0;

    public ICommand ClearCommand { get; }

    public ICommand ExportCommand { get; }

    private void Clear()
    {
        _services.Log.Clear();
        _services.Log.Info("Log", "Log cleared by user.");
    }

    private void Export()
    {
        string defaultDirectory = string.IsNullOrWhiteSpace(_services.Settings.LogExportDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : _services.Settings.LogExportDirectory;

        string fileName = $"golem-tablet-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        string? path = _dialogs.PickSaveFile("Export log", "Log files (*.log)|*.log|All files (*.*)|*.*",
            Path.Combine(defaultDirectory, fileName));
        if (path is null)
        {
            return;
        }

        try
        {
            _services.Log.Export(path);
            _services.Log.Info("Log", "Log exported to " + path);
            _dialogs.ShowInfo("Export log", "Log written to:" + Environment.NewLine + path);
        }
        catch (Exception ex)
        {
            _services.Log.Error("Log", "Export failed: " + ex.Message);
            _dialogs.ShowInfo("Export log", "Export failed: " + ex.Message);
        }
    }
}
