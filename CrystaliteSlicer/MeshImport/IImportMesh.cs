using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.MeshImport
{
    public interface IImportMesh
    {
        public IImportMesh ImportMesh(string path);
        public IImportMesh ApplyTransform();

        public IEnumerable<Triangle> TransformedMesh { get; set; }
        public IEnumerable<Triangle> OriginalMesh { get; set; }
    }
}
