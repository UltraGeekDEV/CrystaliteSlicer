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
        public int gridSizeX;
        public int gridSizeY;

        public GridInfill(int gridSizeX, int gridSizeY)
        {
            this.gridSizeX = gridSizeX;
            this.gridSizeY = gridSizeY;
        }

        public bool IsFill(float distanceToWall, float distanceToCeiling, int x, int y, int z)
        {
            return (x % Math.Max((int)((1 - Settings.InfillDensity) * gridSizeX), 1) == 0 || y % Math.Max((int)((1 - Settings.InfillDensity) * gridSizeY), 1) == 0) && !(x % Math.Max((int)((1 - Settings.InfillDensity) * gridSizeX), 1) == 0 && y % Math.Max((int)((1 - Settings.InfillDensity) * gridSizeY), 1) == 0);
        }

    }
}
