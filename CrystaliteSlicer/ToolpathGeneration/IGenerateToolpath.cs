using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.ToolpathGeneration
{
    public interface IGenerateToolpath
    {
        public IGenerateToolpath SplitLayers(IVoxelCollection voxels);
        public IEnumerable<Line> GetPath();
    }
}
