using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public interface IPostprocessToolpath
    {
        public List<Line> Process(List<Line> path);
    }
}
