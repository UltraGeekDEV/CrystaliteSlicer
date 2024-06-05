using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
