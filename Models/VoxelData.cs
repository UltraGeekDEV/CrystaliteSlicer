using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public struct VoxelData
    {
        public static int fillVoxelValue = int.MaxValue;
        public static int shellVoxelValue = 1;
        public int depth;
    }
}
