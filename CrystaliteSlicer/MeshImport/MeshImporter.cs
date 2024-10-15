using Assimp;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.MeshImport
{
    public class MeshImporter : IImportMesh
    {
        public List<Triangle> ImportMesh(Uri path)
        {
            AssimpContext context = new AssimpContext();
            var scene = context.ImportFile(Uri.UnescapeDataString(path.AbsolutePath), PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices);
            return scene.Meshes.SelectMany(x =>
            {
                var vertices = x.Vertices.Select(x => new Vector3(x.X, x.Y, x.Z)).ToArray();
                return x.Faces.Select(x => new Triangle(vertices[x.Indices[0]], vertices[x.Indices[1]], vertices[x.Indices[2]]));
            }).ToList();
        }
    }
}
