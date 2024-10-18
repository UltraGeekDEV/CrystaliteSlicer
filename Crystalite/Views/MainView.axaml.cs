using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Crystalite.Utils;
using Crystalite.ViewModels;
using System;
using System.Diagnostics;

namespace Crystalite.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        DataContext = new MainViewModel();
        InitializeComponent();
    }
    public void Button_Click(object sender, RoutedEventArgs args)
    {
        Slicer.Slice();
    }
}
