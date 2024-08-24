using Models;
using Models.GcodeInfo;
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

            var paddingElement = path.Last().Flip();
            path.Concat(new List<Line>() { paddingElement });
            
            int mergedCount = 0;
            Line line = path.First();
            List<Line> finalPath = new List<Line>();
            Vector3 initialDir = Vector3.Normalize(line.End - line.Start);

            foreach (var item in path.Skip(1))
            {
                if (!IsSpecialLine(item))
                {
                    if ((line.End - item.Start).Length() < Settings.Resolution.Length() && mergedCount < Settings.SmoothingCount && !item.Travel && !line.Travel && Vector3.Dot(initialDir, Vector3.Normalize(item.End - item.Start)) >= Settings.SmoothingAngle)
                    {
                        line.End = item.End;
                        line.Flow = (line.Flow * (mergedCount + 1) + item.Flow) / (mergedCount + 2);
                        mergedCount++;
                    }
                    else
                    {
                        finalPath.Add(line);
                        mergedCount = 0;
                        line = item;
                        initialDir = Vector3.Normalize(line.End - line.Start);
                    }
                }
                else
                {
                    finalPath.Add(item);
                }
            }

            return finalPath;
        }

        private bool IsSpecialLine(Line line)
        {
            return line is InfoLine;
        }
    }
}
