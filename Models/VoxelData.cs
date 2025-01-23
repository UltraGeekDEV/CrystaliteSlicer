using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public struct VoxelData
    {
        private short depth;
        private short layer;

        public int Depth { get => depth; set => depth = (short)Math.Min(value,short.MaxValue); }
        public int Layer { get => layer; set => layer = (short)Math.Min(value,short.MaxValue); }
    }
}
