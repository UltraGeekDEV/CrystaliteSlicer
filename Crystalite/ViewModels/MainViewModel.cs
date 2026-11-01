using Crystalite.Utils;
using Models;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Crystalite.ViewModels;

public class MainViewModel : ReactiveObject
{
    public bool HasGCode { get => hasGCode; set => this.RaiseAndSetIfChanged(ref hasGCode, value); }

    private bool hasGCode;
    public MainViewModel()
    {
        LoadSettings();
    }

    public static void SaveSettings()
    {
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "Crystalite");
        Directory.CreateDirectory(appData);
        File.WriteAllText(Path.Combine(appData, "LastUsedSettings.json"), JsonConvert.SerializeObject(Settings.Instance, Formatting.Indented));
    }
    private void LoadSettings()
    {
        string settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "Crystalite", "LastUsedSettings.json");
        try
        {
            if (File.Exists(settingsFile))
            {
                Settings.Instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsFile)) ?? throw new InvalidDataException("Empty settings file");
                return;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Ignoring unreadable {settingsFile}: {e.Message}");
        }
        var defaults = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultEnder3.json");
        Settings.Instance = (File.Exists(defaults) ? JsonConvert.DeserializeObject<Settings>(File.ReadAllText(defaults)) : null) ?? new Settings();
        Debug.WriteLine("Defaults loaded for a stock Ender3");
        SaveSettings();
    }
}
