using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.LayerGeneration
{
    internal interface IGenerateLayers
    {
        public void GetLayers(IVoxelCollection voxels);
    }
}
