using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Crystalite.Models;
using Crystalite.ViewModels;
using Crystalite.Views;
using CrystaliteSlicer.GcodeGeneration;
using CrystaliteSlicer.InfillGeneration;
using CrystaliteSlicer.LayerGeneration;
using CrystaliteSlicer.MeshImport;
using CrystaliteSlicer.Postprocessing;
using CrystaliteSlicer.ToolpathGeneration;
using CrystaliteSlicer.Voxelize;
using Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Crystalite.Utils
{
    public static class Slicer
    {
        private static MeshData models = new MeshData();
        private static Mesh buildPlate = new Mesh();

        public static void Setup()
        {
            ConstructBuildPlate();
            Addhandles();
            Sun.Setup();
            MeshData.instance = models;
            CameraData.instance.targetPos = new Vector3(Settings.PrintVolume.X * 0.05f, 0, -Settings.PrintVolume.Z * 0.05f);
        }

        public static void ImportMesh(Uri path)
        {
            var mesh = new MeshImporter().ImportMesh(path);

            OpenGLUtils.QueueAction(() =>
            {
                var importedMesh = new Mesh(mesh, ShaderType.lit);
                importedMesh.col = new OpenTK.Mathematics.Vector3(0.9372f, 0.3254f+0.1f, 0.0666f + 0.05f)*1.4f;
                var pos = (importedMesh.upperRight - importedMesh.lowerLeft) * 0.5f;
                pos += Settings.PrintVolume*new Vector3(0.05f,0,-0.05f);
                pos.Y = -importedMesh.lowerLeft.Y;
                importedMesh.translation = Matrix4x4.CreateTranslation(pos);
                models.models.Add(importedMesh);
            });

        }

        private static void ConstructBuildPlate()
        {
            List<Triangle> tris;
            float plateThickness = 5;
            var plateSize = Settings.PrintVolume;
            plateSize.Z = -plateThickness;

            tris = GetUnitCube();
            foreach (Triangle triangle in tris)
            {
                triangle.Scale(plateSize);
            }
            buildPlate = new Mesh(tris, ShaderType.lit);
            buildPlate.col = new OpenTK.Mathematics.Vector3(0.55f, 0.5f, 0.45f);
            models.staticUI.Add(buildPlate);

            float xyzmarkerThickness = 2;
            var scale = new Vector3(5 * plateThickness, xyzmarkerThickness, -xyzmarkerThickness);
            tris = GetUnitCube();
            foreach (Triangle triangle in tris)
            {
                triangle.Scale(scale);
                triangle.Offset(new Vector3(0, -xyzmarkerThickness, +xyzmarkerThickness));
            }

            var xMarker = new Mesh(tris, ShaderType.unlit);
            xMarker.col = new OpenTK.Mathematics.Vector3(1,0.3f, 0.3f);
            models.staticUI.Add(xMarker);

            scale = new Vector3(xyzmarkerThickness, 5 * plateThickness, -xyzmarkerThickness);
            tris = GetUnitCube();
            foreach (Triangle triangle in tris)
            {
                triangle.Scale(scale);
                triangle.Offset(new Vector3(-xyzmarkerThickness, 0, +xyzmarkerThickness));
            }

            var yMarker = new Mesh(tris, ShaderType.unlit);
            yMarker.col = new OpenTK.Mathematics.Vector3(0.3f, 1, 0.3f);
            models.staticUI.Add(yMarker);

            scale = new Vector3(xyzmarkerThickness,  xyzmarkerThickness, -5*plateThickness-xyzmarkerThickness);
            tris = GetUnitCube();
            foreach (Triangle triangle in tris)
            {
                triangle.Scale(scale);
                triangle.Offset(new Vector3( - xyzmarkerThickness, - xyzmarkerThickness, 5*plateThickness+2*xyzmarkerThickness));
            }

            var zMarker = new Mesh(tris, ShaderType.unlit);
            zMarker.col = new OpenTK.Mathematics.Vector3(0.3f, 0.3f, 1);
            models.staticUI.Add(zMarker);

            scale = new Vector3(xyzmarkerThickness, xyzmarkerThickness, -xyzmarkerThickness);
            tris = GetUnitCube();
            foreach (Triangle triangle in tris)
            {
                triangle.Scale(scale);
                triangle.Offset(new Vector3(-xyzmarkerThickness, -xyzmarkerThickness, xyzmarkerThickness));
            }

            var cornerMarker = new Mesh(tris, ShaderType.unlit);
            cornerMarker.col = new OpenTK.Mathematics.Vector3(1, 1, 1);
            models.staticUI.Add(cornerMarker);

        }
        private static void Addhandles()
        {
            var xHandle = new AxisMesh(new Vector3(1.0f, 0.0f, 0.0f),
                new MeshImporter().ImportMesh(Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Arrow.stl")).Select(x => { x.Offset(new Vector3(0.0f, 1.0f, 0.0f)); return x; })
                ,ShaderType.unlit,0.1f,true);
            xHandle.rotation = Matrix4x4.CreateFromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f),-float.Pi*0.5f);
            models.translationHandles.Add(xHandle);

            var yHandle = new AxisMesh(new Vector3(0.0f, 0.0f, 1.0f),
                new MeshImporter().ImportMesh(Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "Arrow.stl")).Select(x => { x.Offset(new Vector3(0.0f, 1.0f, 0.0f)); return x; })
                , ShaderType.unlit, 0.1f, true);
            models.translationHandles.Add(yHandle);
        }
        public static List<Triangle> GetUnitCube()
        {
            var verts = new Vector3[]
            {
                new Vector3(0,0,0),
                new Vector3(1,0,0),
                new Vector3(1,1,0),
                new Vector3(0,1,0),
                new Vector3(0,0,1),
                new Vector3(1,0,1),
                new Vector3(1,1,1),
                new Vector3(0,1,1),
            };

            var tris = new List<Triangle>()
            {
                //Top
                new Triangle(verts[0],verts[1],verts[2]),
                new Triangle(verts[0],verts[2],verts[3]),
                //Bottom
                new Triangle(verts[4],verts[6],verts[5]),
                new Triangle(verts[4],verts[7],verts[6]),
                //Front
                new Triangle(verts[0],verts[4],verts[5]),
                new Triangle(verts[0],verts[5],verts[1]),
                //Right
                new Triangle(verts[1],verts[5],verts[2]),
                new Triangle(verts[2],verts[5],verts[6]),
                //Back
                new Triangle(verts[3],verts[2],verts[7]),
                new Triangle(verts[2],verts[6],verts[7]),
                //Left
                new Triangle(verts[0],verts[3],verts[4]),
                new Triangle(verts[4],verts[3],verts[7]),
            };
            return tris;
        }

        internal async static void Slice()
        {
            MainViewModel.SaveSettings();
            if (MeshData.instance.models.Count == 0)
            {
                return;
            }

            var meshes = MeshData.instance.models.SelectMany(x => x.GetPrintspacetriangles().Select(y => Reorient(y)));
            //Voxelize
            IVoxelCollection voxels = new Voxelizer().Voxelize(meshes);
            Debug.WriteLine("Voxelized");
            //Generate Layers
            new GenerateLayers().GetLayers(voxels);
            Debug.WriteLine("Generated Layers");
            //Get Toolpath and Apply Infill
            IInfill infill = new GridInfill();
            var toolpath = new NearestNeighborToolpath(voxels,infill).SplitLayers(voxels).GetPath();
            Debug.WriteLine("Toolpath Generated");
            Settings.SmoothingAngle = 0.2f;
            Settings.SmoothingCount = 1;

            toolpath = new SmoothPath().Process(toolpath);
            Settings.SmoothingCount = 6;
            Settings.SmoothingAngle = 0.75f;
            toolpath = new SmoothPath().Process(toolpath);
            toolpath = new PathFiller().Process(toolpath);
            toolpath = new SafeTravel().Process(toolpath);
            toolpath = new PurgeLine().Process(toolpath);
            File.WriteAllLines( "./Test.gcode", new MarlinGCodeGenerator().GetGcode(toolpath.ToList()));
        }

        private static Triangle Reorient(Triangle triangle)
        {
            return new Triangle(Mesh.Deorient(triangle.a), Mesh.Deorient(triangle.b), Mesh.Deorient(triangle.c));
        }
    }
}
