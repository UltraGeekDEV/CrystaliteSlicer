using Avalonia.Controls;
using Avalonia.Input;
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

}
