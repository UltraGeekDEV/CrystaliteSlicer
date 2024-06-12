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
        private IEnumerable<Triangle> transformedMesh;
        private IEnumerable<Triangle> originalMesh;

        public IEnumerable<Triangle> TransformedMesh { get => transformedMesh; set => transformedMesh = value; }
        public IEnumerable<Triangle> OriginalMesh { get => originalMesh; set => originalMesh = value; }

        public IImportMesh ImportMesh(string path)
        {
            AssimpContext context = new AssimpContext();
            var scene = context.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.FixInFacingNormals);
            transformedMesh = originalMesh = scene.Meshes.SelectMany(x =>
            {
                var vertices = x.Vertices.Select(x => new Vector3(x.X, x.Y, x.Z)).ToArray();
                return x.Faces.Select(x => new Triangle(vertices[x.Indices[0]], vertices[x.Indices[1]], vertices[x.Indices[2]]));
            }).ToList();

            return this;
        }

        public IImportMesh ApplyTransform()
        {
            var eulerAngles = Settings.Rotation;
            eulerAngles *= MathF.PI / 180.0f;
            var rotQuaternion = System.Numerics.Quaternion.CreateFromYawPitchRoll(eulerAngles.X,eulerAngles.Y,eulerAngles.Z);
            transformedMesh = originalMesh.Select(x =>
            {
                var tri = x.Copy();
                tri.Rotate(rotQuaternion);
                tri.Scale(Settings.Scale);
                tri.Offset(Settings.Offset);
                return tri;
            }).ToList();
            return this;
        }

    }
}
