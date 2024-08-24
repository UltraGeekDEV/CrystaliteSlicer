using Models;
using Models.GcodeInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public class PathFiller : IPostprocessToolpath
    {
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            var finalPath = new List<Line>() { path.First()};
            var prev = path.Skip(1).First();
            foreach (var item in path.Skip(2))
            {
                if (!(item is InfoLine))
                {
                    finalPath.Add(prev);
                    if ((prev.End - item.Start).Length() > Settings.Resolution.Length() * 0.75)
                    {
                        finalPath.Add(new Line(prev.End, item.Start, 0, true));
                    }
                    prev = item;
                }
                else
                {
                    finalPath.Add(item);
                }
            }
            finalPath.Add(prev);
            return finalPath;
        }
    }
}
