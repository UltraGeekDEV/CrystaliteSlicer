using CrystaliteSlicer.ToolpathGeneration;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.LayerGeneration
{
    public interface IGenerateLayers
    {
        public void GetLayers(IVoxelCollection voxels, IGenerateToolpath toolpathGenerator);
    }
}
