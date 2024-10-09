using Crystalite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public class MeshData
    {
        public static MeshData instance;

        public List<Mesh> meshes = new List<Mesh>();
        static MeshData()
        {
        }
        
    }
}
