using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Crystalite.Utils;
using System.Diagnostics;

namespace Crystalite.Views;

public partial class MainWindow : Window
{
    public static MainWindow instance;
    public MainWindow()
    {
        InitializeComponent(true,true);
        instance = this;
        AddHandler(DragDrop.DropEvent, Drop);
    }
    private void Drop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        foreach (var item in files)
        {
            Slicer.
        }
    }
}
