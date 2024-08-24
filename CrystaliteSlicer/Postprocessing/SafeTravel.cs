using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public class SafeTravel : IPostprocessToolpath
    {
        float zVoxelsPerX;
        public SafeTravel()
        {
            zVoxelsPerX = (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X / Settings.Resolution.Z);
        }
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            var finalPath = new List<Line>();

            foreach (var line in path)
            {
                if (line.Travel)
                {
                    var start = line.Start;
                    var end = line.End;

                    var absoluteMax = (start - end).Length()* zVoxelsPerX;
                    var avgZ = Math.Abs(start.Z-end.Z)*0.5f;
                    var combined = avgZ + absoluteMax * 0.5f;

                    var ratio = combined / absoluteMax;

                    var midPoint = (end-start) * ratio;
                    midPoint.Z = combined;
                    midPoint = start + midPoint;

                    finalPath.Add(new Line(start,midPoint,0,true));
                    finalPath.Add(new Line(midPoint,end,0,true));
                }
                else
                {
                    finalPath.Add(line);
                }
            }

            return finalPath;
        }
    }
}
