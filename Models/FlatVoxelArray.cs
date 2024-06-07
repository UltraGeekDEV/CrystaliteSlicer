using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class FlatVoxelArray : IVoxelCollection
    {
        public VoxelData this[Vector3Int pos] { get => voxels[pos.X+pos.Y*size.Y+pos.Z*Size.Y*Size.Z]; set => voxels[pos.X + pos.Y * size.Y + pos.Z * Size.Y * Size.Z] = value; }
        public VoxelData this[int x, int y, int z] { get => voxels[x + y * size.Y + z * Size.Y * Size.Z]; set => voxels[x + y * size.Y + z * Size.Y * Size.Z] = value; }
        public Vector3Int Size { get => size; set => size = value; }

        private VoxelData[] voxels;
        private Vector3Int size;

        public FlatVoxelArray(Vector3Int size)
        {
            this.size = size;
            voxels = new VoxelData[size.X * size.Y * size.Z];
        }

        public bool Contains(Vector3Int id)
        {
            bool ret = true;
            ret &= id.X >= 0;
            ret &= id.Y >= 0;
            ret &= id.Z >= 0;
            ret &= id.X < size.X;
            ret &= id.Y < size.Y;
            ret &= id.Z < size.Z;
            ret &= this[id].depth != -1;
            return ret;
        }

        public IEnumerable<Vector3Int> GetAllActiveVoxels()
        {
            var ret = new List<Vector3Int>();
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        Vector3Int check = new Vector3Int(i, j, z);
                        if (Contains(check))
                        {
                            ret.Add(check);
                        }
                    }
                }
            }
            return ret;
        }
    }
}
