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
        public void AddLayer((int height,int thickness)[,] layer);
        public IEnumerable<Line> GetPath();
    }
}
