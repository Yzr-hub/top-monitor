using System.ComponentModel;
using System.Windows;
using TopMonitor.App.ViewModels;

namespace TopMonitor.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public bool AllowClose { get; set; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Closing += OnClosing;
    }

    public void ShowAndActivate()
    {
        _viewModel.SetPreviewActive(true);
        Show();
        Activate();
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        _viewModel.SetPreviewActive(false);
        if (AllowClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        Hide();
    }
}
