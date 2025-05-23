using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public class ZOffset : IPostprocessToolpath
    {
        Vector3 offset;
        public ZOffset(float ZOffset)
        {
            this.offset = new Vector3(0, 0, ZOffset);
        }
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            return path.Select(x => new Line(x.Start + offset, x.End + offset, x.Flow, x.Travel));
        }
    }
}
