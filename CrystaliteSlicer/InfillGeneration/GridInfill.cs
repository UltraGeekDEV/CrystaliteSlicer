using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.InfillGeneration
{
    public class GridInfill : IInfill
    {
        int stepSize;

        public GridInfill()
        {
            stepSize = (int)(2 * Settings.NozzleDiameter / (Settings.Resolution.X*Settings.InfillDensity));
        }

        public bool IsFill(float distanceToWall, float distanceToCeiling, int x, int y, int z)
        {
            return x % stepSize == 0 ^ y % stepSize == 0;
        }

    }
}
