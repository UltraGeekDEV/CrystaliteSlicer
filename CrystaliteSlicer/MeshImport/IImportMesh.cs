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
        public IImportMesh ApplyTransform(Vector3 eulerAngles, Vector3 scale, Vector3 offset);
    }
}
