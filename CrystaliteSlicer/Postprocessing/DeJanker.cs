using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Postprocessing
{
    public class DeJanker : IPostprocessToolpath
    {
        public IEnumerable<Line> Process(IEnumerable<Line> incommingPath)
        {
            var path = incommingPath.ToList();
            var retPath = new List<Line>();
            float maxDist = Settings.Resolution.Length()*1.25f;

            Line prev = path.First();
            int id = 0;
            bool flipNext = false;
            foreach (var line in path.Skip(1))
            {
                if (line.Travel)
                {
                    if (id < path.Count-1)
                    {
                        var next = path[id + 1];
                        var dirNext = Vector3.Normalize(next.End - next.Start);
                        var dirPrev = Vector3.Normalize(prev.End - prev.Start);
                        var dirCur = Vector3.Normalize(line.End - line.Start);
                        if (prev.Distance(next) < maxDist && Vector3.Dot(dirNext,dirPrev) > 0 && Vector3.Dot(dirCur,dirPrev) < 0)
                        {
                            retPath.Add(prev.Flip());
                            flipNext = true;
                        }
                        else if (flipNext)
                        {
                            retPath.Add(prev.Flip());
                            flipNext = false;
                        }
                    }
                }
                else
                {
                    if (flipNext)
                    {
                        if (!prev.Travel)
                        {
                            retPath.Add(prev.Flip());
                            flipNext = false;
                        }
                    }
                    else
                    {
                        retPath.Add(prev);
                    }
                }

                prev = line;
            }

            if (flipNext)
            {
                retPath.Add(prev.Flip());
            }
            else
            {
                retPath.Add(prev);
            }

            return retPath;
        }
    }
}
