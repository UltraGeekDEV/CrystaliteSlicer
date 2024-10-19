using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public interface IVoxelCollection
    {
        public VoxelData this[Vector3Int pos] { get;set; }
        public VoxelData this[int x,int y,int z] { get;set; }
        public Vector3Int Size { get; set; }
        public Vector3 LowerLeft { get; set; }
        public int LayerCount { get; set; }

        public bool Contains(Vector3Int id);
        public bool WithinBounds(Vector3Int id);

        public IEnumerable<Vector3Int> GetAllActiveVoxels();
    }
}
