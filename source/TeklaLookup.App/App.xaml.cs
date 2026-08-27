using System;
using System.Windows;
using TeklaLookup.App.Services;
using TeklaLookup.App.ViewModels;
using TeklaLookup.App.Views;

namespace TeklaLookup.App;

public partial class App : Application
{
    public App()
    {
        SettingsStore.Load();
        PinnedAttributeStore.Load();
        // Start template-attribute discovery now, off-thread: it probes every XS_* search path
        // with Directory.Exists, and an unreachable network share in XS_FIRM would otherwise
        // freeze the UI on the first decomposition instead of resolving during startup.
        Services.TemplateAttributes.TemplateAttributeCatalogProvider.Prime();
        Exit += OnExit;
    }

    /// <summary>
    /// Applies the persisted theme after the app's <see cref="Application.Resources"/> have loaded
    /// (i.e. after <c>InitializeComponent</c> in <c>App.g.cs</c>). Doing this in the constructor is
    /// too early: <c>App.xaml</c>'s <c>ThemesDictionary Theme="Light"</c> would later overwrite the
    /// merged dictionary and leave the theme manager out of sync.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!string.IsNullOrEmpty(SettingsStore.Current.Theme)
            && Enum.TryParse<Wpf.Ui.Appearance.ApplicationTheme>(SettingsStore.Current.Theme, true, out var theme))
        {
            try { Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme); }
            catch { /* theme apply is best-effort at startup */ }
        }
    }

    /// <summary>
    /// Safety net: if any monitor windows are still open when the process is shutting down (e.g. an
    /// unhandled exception during normal close), force their <see cref="EventMonitorViewModel"/>
    /// to dispose, which unregisters Tekla's event subscriptions. Without this, Tekla can keep
    /// dispatching to dead delegates and we risk dangling listener references on its side.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        foreach (Window window in Windows)
        {
            if (window is EventMonitorWindow { DataContext: EventMonitorViewModel vm })
                vm.Dispose();
        }
    }
}
