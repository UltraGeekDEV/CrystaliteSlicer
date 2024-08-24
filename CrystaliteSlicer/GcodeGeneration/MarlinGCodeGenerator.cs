using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.GcodeGeneration
{
    public class MarlinGCodeGenerator : IConvertGcode
    {
        public List<string> GetGcode(IEnumerable<Line> path)
        {
            List<string> ret = new List<string>();

            ret.Add($"F{Settings.OuterWallSpeed * 60}");

            var startPos = path.First().Start;
            ret.Add($"G0 X{startPos.X.ToString("0.####")} Y{startPos.Y.ToString("0.####")} Z{startPos.Z.ToString("0.####")}");

            double extrusion = 0;
            double extrusionConstant = 4 * Settings.NozzleDiameter * Settings.Resolution.Z / (Math.PI * Math.Pow(1.75, 2));
            int layer = 1;
            foreach (Line line in path)
            {
                if (line.Travel)
                {
                    if (line.Flow < 0)
                    {
                        ret.Add($";LAYER:{layer++}");
                    }
                    ret.Add($"G0 X{line.End.X.ToString("0.####")} Y{line.End.Y.ToString("0.####")} Z{line.End.Z.ToString("0.####")}");
                }
                else
                {
                    extrusion += line.Flow * line.Length() * extrusionConstant;
                    ret.Add($"G1 X{line.End.X.ToString("0.####")} Y{line.End.Y.ToString("0.####")} Z{line.End.Z.ToString("0.####")} E{extrusion.ToString("0.####")}");
                }
            }

            return ret;
        }
    }
}
