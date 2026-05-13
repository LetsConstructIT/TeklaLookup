using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TeklaLookup.App.Models;
using TeklaLookup.App.Services;

namespace TeklaLookup.App.ViewModels;

public sealed class EventMonitorViewModel : BaseViewModel, IDisposable
{
    private readonly EventMonitor _monitor = new();
    private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher
                                              ?? Dispatcher.CurrentDispatcher;
    private bool _isListening;

    public EventMonitorViewModel()
    {
        StartCommand = new RelayCommand(_ => Start(), _ => !IsListening);
        StopCommand  = new RelayCommand(_ => Stop(),  _ => IsListening);
        ClearCommand = new RelayCommand(_ => Entries.Clear());
        SelectInTeklaCommand = new RelayCommand(
            p => SelectInTekla(p),
            p => CountSelectable(p) > 0);

        _monitor.EntryLogged += OnEntryLogged;
    }

    public ICommand SelectInTeklaCommand { get; }

    private readonly Tekla.Structures.Model.Model _model = new();

    private void SelectInTekla(object? parameter)
    {
        var entries = ExtractEntries(parameter);
        var resolved = new ArrayList();
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Guid)) continue;
            try
            {
                var identifier = _model.GetIdentifierByGUID(entry.Guid);
                var modelObject = _model.SelectModelObject(identifier);
                if (modelObject != null)
                    resolved.Add(modelObject);
            }
            catch { /* object may have been deleted since the event fired */ }
        }

        if (resolved.Count == 0) return;
        try { new Tekla.Structures.Model.UI.ModelObjectSelector().Select(resolved); }
        catch { /* swallow — selection is best-effort */ }
    }

    private static IEnumerable<EventLogEntry> ExtractEntries(object? parameter)
    {
        if (parameter is EventLogEntry single)
            return new[] { single };
        if (parameter is IEnumerable enumerable)
        {
            var list = new System.Collections.Generic.List<EventLogEntry>();
            foreach (var item in enumerable)
                if (item is EventLogEntry e) list.Add(e);
            return list;
        }
        return System.Array.Empty<EventLogEntry>();
    }

    private static int CountSelectable(object? parameter)
    {
        var count = 0;
        foreach (var entry in ExtractEntries(parameter))
            if (!string.IsNullOrEmpty(entry.Guid)) count++;
        return count;
    }

    public ObservableCollection<EventLogEntry> Entries { get; } = new();

    public bool IsListening
    {
        get => _isListening;
        private set => SetProperty(ref _isListening, value);
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand  { get; }
    public ICommand ClearCommand { get; }

    public void Start()
    {
        if (IsListening) return;
        _monitor.Start();
        IsListening = true;
    }

    public void Stop()
    {
        if (!IsListening) return;
        _monitor.Stop();
        IsListening = false;
    }

    public void Dispose()
    {
        _monitor.EntryLogged -= OnEntryLogged;
        _monitor.Dispose();
    }

    private void OnEntryLogged(EventLogEntry entry)
    {
        // Tekla fires events on a background thread — marshal to the UI thread before touching
        // the ObservableCollection.
        if (_dispatcher.CheckAccess())
            Append(entry);
        else
            _dispatcher.BeginInvoke(new Action(() => Append(entry)));
    }

    private void Append(EventLogEntry entry)
    {
        Entries.Add(entry);
    }
}
