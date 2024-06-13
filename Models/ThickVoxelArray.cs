using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class ThickVoxelArray : IVoxelCollection
    {
        public VoxelData this[Vector3Int pos] { get => voxels[pos.X , pos.Y , pos.Z]; set => voxels[pos.X , pos.Y , pos.Z] = value; }
        public VoxelData this[int x, int y, int z] { get => voxels[x , y , z ]; set => voxels[x, y, z] = value; }
        public Vector3Int Size { get => size; set => size = value; }

        private VoxelData[,,] voxels;
        private Vector3Int size;

        public ThickVoxelArray(Vector3Int size)
        {
            this.size = size;
            voxels = new VoxelData[size.X , size.Y , size.Z];
        }
        public bool WithinBounds(Vector3Int id)
        {
            return id.X >= 0 && id.Y >= 0 && id.Z >= 0 && id.X < size.X && id.Y < size.Y && id.Z < size.Z;
        }
        public bool Contains(Vector3Int id)
        {
            return WithinBounds(id) && this[id].depth != -1;
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

