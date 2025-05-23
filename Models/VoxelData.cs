using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public struct VoxelData
    {
        private short zDepth;
        private short xyDepth;
        private short depth;
        private short layer;

        public int ZDepth { get => zDepth; set => zDepth = (short)Math.Min(value, short.MaxValue); }
        public int XYDepth { get => xyDepth; set => xyDepth = (short)Math.Min(value, short.MaxValue); }
        public int Layer { get => layer; set => layer = (short)Math.Min(value, short.MaxValue); }
        public bool IsAir()
        {
            return ZDepth == -1 || XYDepth == -1;
        }
        public static VoxelData GetAir()
        {
            return new VoxelData() { ZDepth = -1, Layer = 0, xyDepth = -1 };
        }
        public static VoxelData GetSolid()
        {
            return new VoxelData() { ZDepth = 0, Layer = 0 , zDepth = 0 };
        }
    }
}
