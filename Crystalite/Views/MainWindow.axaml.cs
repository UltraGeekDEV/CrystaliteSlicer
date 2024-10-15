using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Crystalite.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Crystalite.Views;

public partial class MainWindow : Window
{
    public static MainWindow instance;
    public MainWindow()
    {
        AddHandler(DragDrop.DropEvent, Drop);
        InitializeComponent(true,true);
        instance = this;
    }
    private void Drop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles().Where(x =>
        {
            var split = x.Name.Split('.');
            if (split.Length <= 1)
            {
                return false;
            }

            var acceptedFileFormats = new List<string>() 
            { 
                "stl","obj"
            };

            return acceptedFileFormats.Contains(split[split.Length-1]);
        });
        foreach (var item in files)
        {
            Slicer.ImportMesh(item.Path);
        }
    }
}
