using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class FlatVoxelArray : IVoxelCollection
    {
        public VoxelData this[Vector3Int pos] { get => voxels[pos.X+pos.Y*size.Y+pos.Z*Size.Y*Size.Z]; set => voxels[pos.X + pos.Y * size.Y + pos.Z * Size.Y * Size.Z] = value }
        public VoxelData this[int x, int y, int z] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Vector3Int Size { get => size; set => size = value; }

        private VoxelData[] voxels;
        private Vector3Int[] voxelsPosition;
        private Vector3Int size;
    }
}
