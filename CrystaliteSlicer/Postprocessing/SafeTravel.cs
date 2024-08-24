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
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            var finalPath = new List<Line>();

            foreach (var line in path)
            {
                if (line.Travel)
                {

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
