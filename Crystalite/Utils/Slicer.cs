using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Crystalite.Models;
using Crystalite.Views;
using CrystaliteSlicer.MeshImport;
using Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public static class Slicer
    {
        private static List<string> files = new List<string>();
        private static MeshData models = new MeshData();
        public static void InitSlicer()
        {
            SelectFile();
            var mesh = new MeshImporter().ImportMesh(files[0]).OriginalMesh;
            models.meshes.Add(new Models.Mesh(mesh));
            mesh = new MeshImporter().ImportMesh(files[0]).OriginalMesh;
            models.meshes.Add(new Models.Mesh(mesh));
            models.meshes[1].transform *= Matrix4x4.CreateTranslation(new Vector3(-5f, 0, 7))*Matrix4x4.CreateFromYawPitchRoll(60*(MathF.PI/180.0f),0,0);
            models.meshes.Add(new Models.Mesh(mesh));
            models.meshes[2].transform *= Matrix4x4.CreateTranslation(new Vector3(-5f, 0, -7)) * Matrix4x4.CreateFromYawPitchRoll(-60 * (MathF.PI / 180.0f), 0, 0);
            models.meshes[0].col = new OpenTK.Mathematics.Vector3(0.9372f, 0.3254f, 0.0666f);
            models.meshes[1].col = new OpenTK.Mathematics.Vector3(1, 0, 0);
            models.meshes[2].col = new OpenTK.Mathematics.Vector3(0, 1, 0);
            MeshData.instance = models;

        }

        public static void SelectFile()
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow.instance);

            var filter = new List<FilePickerFileType>()
                    {
                        new FilePickerFileType("3D Files")
                        {
                            Patterns = new[]{"*.stl","*.obj"}
                        }
                    };

            var selectedFiles = topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions()
                {
                    Title = "Select file",
                    FileTypeFilter = filter,
                    AllowMultiple = false
                }).Result;

            if (selectedFiles.Count != 0)
            {
                var path = Uri.UnescapeDataString(selectedFiles[0].Path.AbsolutePath);
                files.Add(path);
            }
        }
    }
}
