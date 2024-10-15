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
        public List<Triangle> ImportMesh(Uri path);
    }
}
