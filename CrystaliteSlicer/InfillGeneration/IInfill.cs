using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.InfillGeneration
{
    public interface IInfill
    {
        public bool IsFill(float distanceToWall, float distanceToCeiling, int x, int y, int z);
    }
}
