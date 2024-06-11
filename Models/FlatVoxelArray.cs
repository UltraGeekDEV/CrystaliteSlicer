using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class FlatVoxelArray : IVoxelCollection
    {
        public VoxelData this[Vector3Int pos] { get {
                if (ValidID(pos))
                {
                    return voxels[pos.X + pos.Y * size.X + pos.Z * Size.X * Size.Y];
                }
                return new VoxelData() { depth = -1};
            }
            set {
                if (ValidID(pos)) {
                    voxels[pos.X + pos.Y * size.X + pos.Z * Size.X * Size.Y] = value;
                } 
            }
        }
        public VoxelData this[int x, int y, int z] { get => voxels[x + y * size.X + z * Size.X * Size.Y]; set => voxels[x + y * size.X + z * Size.X * Size.Y] = value; }
        public Vector3Int Size { get => size; set => size = value; }

        private VoxelData[] voxels;
        private Vector3Int size;

        public FlatVoxelArray(Vector3Int size)
        {
            this.size = size;
            voxels = new VoxelData[size.X * size.Y * size.Z];
        }
        public bool ValidID(Vector3Int id)
        {
            bool ret = true;
            ret &= id.X >= 0;
            ret &= id.Y >= 0;
            ret &= id.Z >= 0;
            ret &= id.X < size.X;
            ret &= id.Y < size.Y;
            ret &= id.Z < size.Z;
            return ret;
        }
        public bool Contains(Vector3Int id)
        {
            return ValidID(id) && this[id].depth != -1;
        }

        public IEnumerable<Vector3Int> GetAllActiveVoxels()
        {
            var ret = new ConcurrentQueue<Vector3Int>();

            var points = Enumerable.Range(0, Size.X).AsParallel().SelectMany(x => Enumerable.Range(0, Size.Y).SelectMany(y => Enumerable.Range(0, Size.Z).Select(z => new Vector3Int(x, y, z))));

            points.ForAll(check =>
            {
                if (Contains(check))
                {
                    ret.Enqueue(check);
                }
            });
            return ret;
        }
    }
}
