using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Crystalite.Utils;
using Crystalite.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Crystalite.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        DataContext = new MainViewModel();
        InitializeComponent();
    }
    public void Slice_Button_Click(object sender, RoutedEventArgs args)
    {
        if(!Slicer.IsSlicing)
        {
            Task.Run(Slicer.Slice);
        }
    }
    public async void Save_GCode_Button_Click(object sender, RoutedEventArgs args)
    {
        if (Slicer.HasToolpath)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var filePath = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save G-code",
                DefaultExtension = "gcode",
            });

            if (filePath != null)
            {
                Task.Run(() => Slicer.SaveGCode(filePath.Path.AbsolutePath));
            }
        }
    }
}
