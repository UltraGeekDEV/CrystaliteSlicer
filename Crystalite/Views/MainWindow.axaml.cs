using Avalonia;
using Avalonia.Controls;
using Crystalite.Utils;

namespace Crystalite.Views;

public partial class MainWindow : Window
{
    public static MainWindow instance;
    public MainWindow()
    {
        InitializeComponent(true,true);
        instance = this;
        Slicer.InitSlicer();
    }
}
