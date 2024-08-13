using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public class SmoothPath : IPostprocessToolpath
    {
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            int mergedCount = 0;
            Line line = path.First();

            List<Line> finalPath = new List<Line>();

            foreach (var item in path.Skip(1))
            {
                if (mergedCount < Settings.SmoothingCount && line.Travel == item.Travel && Vector3.Dot(Vector3.Normalize(line.End - line.Start), Vector3.Normalize(item.End - item.Start)) >= 0.0f)
                {
                    line.End = item.End;
                    if (item.Travel && (item.Flow < 0 || line.Flow < 0))
                    {
                        line.Flow = -1;
                    }
                    else
                    {
                        line.Flow = (line.Flow * (mergedCount + 1) + item.Flow) / (mergedCount + 2);
                    }
                    mergedCount++;
                }
                else
                {
                    finalPath.Add(line);
                    mergedCount = 0;
                    line = item;
                }
            }
            finalPath.Add(line);

            return finalPath;
        }
    }
}
