using Models;
using Models.GcodeInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public class SlopeAlign : IPostprocessToolpath
    {
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            var fullLine = path.ToList();
            List<Line> finalPath = new List<Line>();

            int i = 0;
            int segmentStart = 0;
            while (i < fullLine.Count)
            {
                if (fullLine[i] is InfoLine || fullLine[i].Travel)
                {
                    finalPath.Add(fullLine[i]);
                }
                else
                {
                    while (i < fullLine.Count-1 && !fullLine[i].Travel && fullLine[i].End.Z == fullLine[segmentStart].End.Z)
                    {
                        i++;
                    }

                    Vector3 SegmentDelta = fullLine[i].End - fullLine[segmentStart].Start;

                    Vector3 slope = new Vector3(SegmentDelta.Z)/new Vector3(SegmentDelta.X, SegmentDelta.Y, 0);

                    for (int j = segmentStart; j < i; j++)
                    {
                        if (!(fullLine[j] is InfoLine))
                        {
                            Vector3 startDist = fullLine[j].Start - fullLine[segmentStart].Start;
                            Vector3 endDist = fullLine[j].End - fullLine[segmentStart].Start;

                            startDist.Z = slope.X * startDist.X + slope.Y * startDist.Y;
                            endDist.Z = slope.X * endDist.X + slope.Y * endDist.Y;

                            finalPath.Add(new Line(
                                fullLine[j].Start+new Vector3(0,0,startDist.Z),
                                fullLine[j].End + new Vector3(0, 0, endDist.Z),
                                fullLine[j].Flow,
                                fullLine[j].Travel));
                        }
                        else
                        {
                            finalPath.Add(fullLine[j]);
                        }
                    }

                }
                

                segmentStart = i++;
            }

            finalPath.Add(fullLine[fullLine.Count - 1]);

            return finalPath;
        }
    }
}
