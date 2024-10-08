using Avalonia.Controls;
using Crystalite.ViewModels;

namespace Crystalite.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        DataContext = new MainViewModel();
        InitializeComponent();
    }
}
