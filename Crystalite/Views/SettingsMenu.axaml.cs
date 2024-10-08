using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Crystalite.ViewModels;

namespace Crystalite;

public partial class SettingsMenu : UserControl
{
    public SettingsMenu()
    {
        DataContext = new SettingsViewModel();
        InitializeComponent();
    }
}