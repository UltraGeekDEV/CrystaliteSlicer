using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class FlatVoxelArray : IVoxelCollection
    {
        private int zMult;
        public VoxelData this[Vector3Int pos] { get => voxels[pos.X+pos.Y*size.X+pos.Z* zMult]; set => voxels[pos.X + pos.Y * size.X + pos.Z * zMult] = value; }
        public VoxelData this[int x, int y, int z] { get => voxels[x + y * size.X + z * zMult]; set => voxels[x + y * size.X + z * zMult] = value; }
        public Vector3Int Size { get => size; set => size = value; }
        public int LayerCount { get => layerCount; set => layerCount = value; }
        public Vector3 LowerLeft { get; set ; }

        private int layerCount;
        private VoxelData[] voxels;
        private Vector3Int size;

        public FlatVoxelArray(Vector3Int size)
        {
            this.size = size;
            voxels = new VoxelData[size.X * size.Y * size.Z];
            zMult = size.X * size.Y;
        }
        public bool WithinBounds(Vector3Int id)
        {
            return id.X >= 0 && id.Y >= 0 && id.Z >= 0 && id.X < size.X && id.Y < size.Y && id.Z < size.Z;
        }
        public bool Contains(Vector3Int id)
        {
            return WithinBounds(id) && this[id].Depth != IVoxelCollection.airVoxel;
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
