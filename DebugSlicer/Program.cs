using ConsoleTools;
using CrystaliteSlicer.InfillGeneration;
using CrystaliteSlicer.LayerGeneration;
using CrystaliteSlicer.MeshImport;
using CrystaliteSlicer.Postprocessing;
using CrystaliteSlicer.ToolpathGeneration;
using CrystaliteSlicer.Voxelize;
using Models;
using Newtonsoft.Json;
using System.CodeDom.Compiler;
using System.Numerics;

namespace DebugSlicer
{
    internal class Program
    {
        static string filePath = "";
        static string fileNoExtension = "";
        [STAThread]
        static void Main(string[] args)
        {
            LoadSettings();
            Settings.SmoothingAngle = 0.0f;
            Settings.SmoothingCount = 2;
            Settings.OverhangOverlap = 0.0f;

            var ModelSettingsMenu = new ConsoleMenu()
                .Add("Uniform Scale",() => { Settings.Scale = Vector3.One * ReadFloatMenu("Scale Multiplier"); SaveSettings(); })
                .Add("Non-Uniform Scale",() => { Settings.Scale = new Vector3(ReadFloatMenu("X Scale"), ReadFloatMenu("Y Scale"), ReadFloatMenu("Z Scale")); SaveSettings(); })
                .Add("Rotation",() => { Settings.Rotation = new Vector3(ReadFloatMenu("X Rotation"), ReadFloatMenu("Y Rotation"), ReadFloatMenu("Z Rotation")); SaveSettings(); })
                .Add("Position Offset",() => { Settings.Offset = new Vector3(ReadFloatMenu("X Offset"), ReadFloatMenu("Y Offset"), ReadFloatMenu("Z Offset")); SaveSettings(); })
                .Add("Back",(x) => x.CloseMenu());

            var PrinterSettingsMenu = new ConsoleMenu()
                .Add("Print Volume", () => { Settings.PrintVolume = new Vector3(ReadFloatMenu("X Size"), ReadFloatMenu("Y Size"), ReadFloatMenu("Z Size")); SaveSettings(); })
                .Add("Nozzle Diameter", () => { Settings.NozzleDiameter = ReadFloatMenu("Nozzle Diameter"); SaveSettings(); })
                .Add("Max Slope", () => { Settings.MaxSlope = ReadFloatMenu("Max Slope", 0.001f, 30.0f); SaveSettings(); })
                .Add("Back", (x) => x.CloseMenu());

            var QualitySettingsMenu = new ConsoleMenu()
                .Add("Horizontal Resolution", () => { var res = ReadFloatMenu("Horizontal Resolution", 0.01f, Settings.NozzleDiameter); Settings.Resolution = new Vector3(res, res, Settings.Resolution.Z); })
                .Add("Vertical Resolution", () => { Settings.Resolution = new Vector3(Settings.Resolution.X, Settings.Resolution.Y, ReadFloatMenu("Z Resolution", 0.01f, (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X / Settings.Resolution.Z))); SaveSettings(); })
                .Add("Max Layer Height", ()=> { Settings.MaxLayerHeight = ReadFloatMenu("Max Layer Height", Settings.Resolution.Z * 1.01f, Settings.PrintVolume.Z); SaveSettings(); })
                .Add("Back", (x)=> x.CloseMenu());

            var WallSettings = new ConsoleMenu()
                .Add("Wall Count", () => { Settings.WallCount = ReadIntMenu("Wall Count"); SaveSettings(); })
                .Add("Top/Bottom Thickness", () => { Settings.TopThickness = ReadFloatMenu("Top/Bottom Thickness"); SaveSettings(); })
                .Add("Infill Density", () => { Settings.InfillDensity = (ReadFloatMenu("Infill Density", 0.001f, 100.0f) / 100.0f); SaveSettings(); })
                .Add("Back",(x) => x.CloseMenu());

            var MaterialSettings = new ConsoleMenu()
                .Add("Hotend Temperature", ()=>{ Settings.HotendTemp = ReadFloatMenu("Hotend Temperature"); SaveSettings(); })
                .Add("Bed Temperature", ()=>{ Settings.BedTemp = ReadFloatMenu("Bed Temperature"); SaveSettings(); })
                .Add("Fan Speed", ()=>{ Settings.BedTemp = ReadFloatMenu("Fan Speed", 0, 100) / 100.0f; SaveSettings(); })
                .Add("Back", (x) => x.CloseMenu());

            var SpeedSettings = new ConsoleMenu()
                .Add("Outer Wall Speed", () => { Settings.OuterWallSpeed = ReadFloatMenu("Outer Wall Speed"); SaveSettings(); })
                .Add("Inner Wall Speed", () => {Settings.InnerWallSpeed = ReadFloatMenu("Inner Wall Speed"); SaveSettings();
        })
                .Add("Infill Speed", () => { Settings.InfillSpeed = ReadFloatMenu("Infill Speed"); SaveSettings(); })
                .Add("Travel Speed", () => { Settings.TravelSpeed = ReadFloatMenu("Travel Speed"); SaveSettings(); })
                .Add("Retraction Distance", () => { Settings.RetractionDistance = ReadFloatMenu("Retraction Distance"); SaveSettings(); })
                .Add("Retraction Speed", () => { Settings.RetractionSpeed = ReadFloatMenu("Retraction Speed"); SaveSettings(); })
                .Add("Back", (x) => x.CloseMenu());

            var PrintSettingsMenu = new ConsoleMenu()
                .Add("Model Settings", ModelSettingsMenu.Show)
                .Add("Printer Settings", PrinterSettingsMenu.Show)
                .Add("Quality Settings", QualitySettingsMenu.Show)
                .Add("Wall and Infill Settings", WallSettings.Show)
                .Add("Material Settings", MaterialSettings.Show)
                .Add("Speed and Travel Settings", SpeedSettings.Show)
                .Add("Load Print Settings", LoadPrintSettings)
                .Add("Save Print Settings", SavePrintSettings)
                .Add("Back",(x) => x.CloseMenu());

            var TimeLapseSettings = new ConsoleMenu()
                .Add("Enhanced Timelapse: OFF",ToggleTimelapse);

            var MainPage = new ConsoleMenu()
                .Add("Select File", SelectFile)
                .Add("Edit Print Settings", PrintSettingsMenu.Show)
                .Add("List Current Settings", ListSettings)
                .Add("Slice", Slice);

            MainPage.Show();
        }

        private static void ToggleTimelapse(ConsoleMenu x)
        {
            //Settings.TimelapseEnabled = !Settings.TimelapseEnabled;

            //if (Settings.TimelapseEnabled)
            //{
            //    x.
            //}
            //else
            //{

            //}
        }

        private static void SaveSettings()
        {
            File.WriteAllText(Application.CommonAppDataPath+"/LastUsedSettings.json",JsonConvert.SerializeObject(Settings.Instance,Formatting.Indented));
        }
        private static void LoadSettings()
        {
            if (File.Exists(Application.CommonAppDataPath + "/LastUsedSettings.json"))
            {
                Settings.Instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Application.CommonAppDataPath + "/LastUsedSettings.json"));
            }
            else
            {
                Settings.Instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Application.StartupPath + "/DefaultEnder3.json"));
                Console.WriteLine("Defaults loaded for a stock Ender3\nPlease make sure to check them before printing\n\nIf you encounter missing/incomplete areas, try reducing vertical resolution\nSupports aren't implemented yet, areas that overhang more than 90° will be ignored!\n\n[Press any button to continue]");
                Console.ReadKey();
                SaveSettings();
            }
        }
        private static void ListSettings()
        {
            Console.Clear();
            Console.WriteLine("\x1b[3J"); //Proper clear

            Console.WriteLine("Model Settings:\n");
            Console.WriteLine("\tScale:");
            Console.WriteLine(GetString(Settings.Scale,""));
            Console.WriteLine("\tRotation:");
            Console.WriteLine(GetString(Settings.Rotation, "°"));
            Console.WriteLine("\tOffset:");
            Console.WriteLine(GetString(Settings.Offset, " mm"));

            Console.WriteLine("\nPrinter Settings:\n");
            Console.WriteLine("\tPrint Volume:");
            Console.WriteLine(GetString(Settings.PrintVolume, " mm"));
            Console.Write("\tNozzle Diameter:");
            Console.WriteLine($" {Settings.NozzleDiameter} mm");
            Console.Write("\tMax Slope:");
            Console.WriteLine($" {Settings.MaxSlope}°");

            Console.WriteLine("\nQuality Settings:\n");
            Console.Write("\tHorizontal Resolution: ");
            Console.WriteLine($"{Settings.Resolution.X} mm");
            Console.Write("\tVertical Resolution: ");
            Console.WriteLine($"{Settings.Resolution.Z} mm");
            Console.Write("\tMax Layer Height: ");
            Console.WriteLine($"{Settings.MaxLayerHeight} mm");

            Console.WriteLine("\nWall and Infill Settings:\n");
            Console.Write("\tWall Count:");
            Console.WriteLine($" {Settings.WallCount}");
            Console.Write("\tTop/Bottom Thickness:");
            Console.WriteLine($" {Settings.TopThickness} mm");
            Console.Write("\tInfill Density:");
            Console.WriteLine($" {Settings.InfillDensity*100}%");
            Console.WriteLine("\tInfill Pattern: Grid (only option currently)");

            Console.WriteLine("\nMaterial Settings:\n");
            Console.WriteLine($"\tHotend Temperature: {Settings.HotendTemp} C°");
            Console.WriteLine($"\tBed Temperature: {Settings.BedTemp} C°");
            Console.WriteLine($"\tFan Speed: {Settings.FanSpeed*100}%");

            Console.WriteLine("\nSpeed and Travel Settings:\n");
            Console.WriteLine($"\tOuter Wall Speed: {Settings.OuterWallSpeed} mm/s");
            Console.WriteLine($"\tInner Wall Speed: {Settings.InnerWallSpeed} mm/s");
            Console.WriteLine($"\tInfill Speed: {Settings.InfillSpeed} mm/s");
            Console.WriteLine($"\tTravel Speed: {Settings.TravelSpeed} mm/s");
            Console.WriteLine($"\tRetraction Distance: {Settings.RetractionDistance} mm");
            Console.WriteLine($"\tRetraction Speed: {Settings.RetractionSpeed} mm/s");

            Console.WriteLine("\n[Press any button to exit]");
            Console.ReadKey();
        }
        private static string GetString(Vector3 vec,string unit)
        {
            return $"\t\tX:\t{vec.X}{unit}\n\t\tY:\t{vec.Y}{unit}\n\t\tZ:\t{vec.Z}{unit}\n";
        }
        private static void SavePrintSettings()
        {
            var sfd = new SaveFileDialog();
            sfd.AddExtension = true;
            sfd.DefaultExt = "json";
            sfd.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            var result = sfd.ShowDialog();
            if (result == DialogResult.OK)
            {
                File.WriteAllText(sfd.FileName, JsonConvert.SerializeObject(Settings.Instance,Formatting.Indented));
            }
        }

        private static void LoadPrintSettings()
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            var result = ofd.ShowDialog();
            if (result == DialogResult.OK)
            {
                var data = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(ofd.FileName));
                Settings.Instance = data;
                SaveSettings();
            }
        }

        private static void SelectFile()
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "stl files (*.stl)|*.stl|All files (*.*)|*.*";
            var result = ofd.ShowDialog();
            if (result == DialogResult.OK)
            {
                filePath = ofd.FileName;
                var split = filePath.Split('.');
                fileNoExtension = split[0];
                for (int i = 1; i < split.Length-1; i++)
                {
                    fileNoExtension += "."+split[i];
                }
            }
        }

        private static void Slice()
        {
            Console.Clear();
            if (!Path.Exists(filePath))
            {
                Console.WriteLine("Please select a file before slicing!\n\n[Press any key to continue]");
                Console.ReadKey();
                return;
            }
            Console.WriteLine("Importing Mesh");
            IEnumerable<Triangle> tris = new MeshImporter().ImportMesh(filePath).ApplyTransform().TransformedMesh;
            Console.WriteLine("Voxelizing Mesh");
            IVoxelCollection voxels = new Voxelizer().Voxelize(tris);
            IInfill infillPattern = new GridInfill();
            IGenerateToolpath toolpathGenerator = new NearestNeighborToolpath(voxels, infillPattern);
            Console.WriteLine("Generating Layers");
            MeasuredRun(() => new GenerateLayers().GetLayers(voxels), "\tLayer Generation");
            IEnumerable<Line> toolpath = new List<Line>();
            Console.WriteLine("Generating Toolpath");
            MeasuredRun(() => ((NearestNeighborToolpath)toolpathGenerator).SplitLayers(voxels), "\tSplitting Layers");
            MeasuredRun(() => toolpath = toolpathGenerator.GetPath(), "\tToolpath Generation");
            //toolpath = new SlopeAlign().Process(toolpath);
            Console.WriteLine("Post processing path");
            toolpath = new SmoothPath().Process(toolpath);
            Settings.SmoothingCount = 6;
            Settings.SmoothingAngle = 0.75f;
            toolpath = new SmoothPath().Process(toolpath);
            toolpath = new PathFiller().Process(toolpath);
            toolpath = new SafeTravel().Process(toolpath);
            toolpath = new PurgeLine().Process(toolpath);
            File.WriteAllLines(fileNoExtension+".gcode", JankGcodegenerator.GetGCode(toolpath.ToList()));
            Console.WriteLine("\nThe gcode file has been exported to the same folder as the original file.\n\n[Press any key to continue]");
            Console.ReadKey();
        }

        private static void MeasuredRun(Action action,string actionName)
        {
            var start = DateTime.Now;

            action.Invoke();

            var total = DateTime.Now-start;

            Console.WriteLine($"{actionName} took :\t{total.TotalSeconds.ToString("0.##")} s");
        }

        private static int ReadIntMenu(string variableName, int min, int max)
        {
            Console.Clear();
            Console.WriteLine(variableName + $" ({min}-{max}):\t");
            while (true)
            {
                try
                {
                    var value = int.Parse(Console.ReadLine());
                    return Math.Clamp(value, min, max);
                }
                catch
                {
                    Console.WriteLine("Invalid number, please press a key and try again");
                    Console.ReadKey();
                }
            }
        }
        private static int ReadIntMenu(string variableName)
        {
            Console.Clear();
            Console.WriteLine(variableName + " :\t");
            while (true)
            {
                try
                {
                    var value = int.Parse(Console.ReadLine());
                    return value;
                }
                catch
                {
                    Console.WriteLine("Invalid number, please press a key and try again");
                    Console.ReadKey();
                }
            }
        }
        private static float ReadFloatMenu(string variableName, float min, float max)
        {
            Console.Clear();
            Console.WriteLine(variableName + $" ({min}-{max}):\t");
            while (true)
            {
                try
                {
                    var value = float.Parse(Console.ReadLine());
                    return Math.Clamp(value,min,max);
                }
                catch
                {
                    Console.WriteLine("Invalid number, please press a key and try again");
                    Console.ReadKey();
                }
            }
        }
        private static float ReadFloatMenu(string variableName)
        {
            Console.Clear();
            Console.WriteLine(variableName + " :\t");
            while (true)
            {
                try
                {
                    var value = float.Parse(Console.ReadLine());
                    return value;
                }
                catch
                {
                    Console.WriteLine("Invalid number, please press a key and try again");
                    Console.ReadKey();
                }
            }
        }
    }
}
