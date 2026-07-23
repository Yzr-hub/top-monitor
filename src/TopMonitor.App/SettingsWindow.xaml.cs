using System.ComponentModel;
using System.Windows;
using TopMonitor.App.ViewModels;

namespace TopMonitor.App;

public partial class SettingsWindow : Window
{
    public bool AllowClose { get; set; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Closing += OnClosing;
    }

    public void ShowAndActivate()
    {
        Show();
        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (AllowClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        Hide();
    }
}
