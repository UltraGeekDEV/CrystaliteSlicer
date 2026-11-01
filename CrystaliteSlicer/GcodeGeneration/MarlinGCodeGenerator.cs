using Models;
using Models.GcodeInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.GcodeGeneration
{
    public class MarlinGCodeGenerator : IConvertGcode
    {
        public List<string> GetGcode(IEnumerable<Line> toolpath)
        {
            List<string> ret = new List<string>();
            var lines = toolpath as IList<Line> ?? toolpath.ToList();

            // Always invariant culture: on e.g. Hungarian locales "{x}" would print "60,5", which no firmware parses.
            static string N(double x) => x.ToString("0.#####", CultureInfo.InvariantCulture);

            double wallFeedrate = Settings.OuterWallSpeed * 60;
            double innerWallFeedrate = Settings.InnerWallSpeed * 60;
            double infillFeedrate = Settings.InfillSpeed * 60;
            double travelFeedrate = Settings.TravelSpeed * 60;
            double retractFeedrate = Settings.RetractionSpeed * 60;

            ret.Add("M82"); //absolute extrusion
            ret.Add("G92 E0"); //reset extruder
            ret.Add("G28"); //home

            ret.Add($"M140 S{N(Settings.BedTemp)}");
            ret.Add($"M105");
            ret.Add($"M190 S{N(Settings.BedTemp)}");
            ret.Add($"M104 S{N(Settings.HotendTemp)}");
            ret.Add($"M105");
            ret.Add($"M109 S{N(Settings.HotendTemp)}");
            ret.Add($"M106 S{(int)(Math.Clamp(Settings.FanSpeed, 0, 1) * 255)}");

            ret.Add($"G0 F{N(travelFeedrate)}");

            var startPos = lines.First(x => !(x is InfoLine)).Start;
            ret.Add($"G0 X{N(startPos.X)} Y{N(startPos.Y)} Z{N(startPos.Z)}");

            double extrusion = 0;
            // Line.Flow is bead height in units of Resolution.Z, so this is width * height / filament area.
            double extrusionConstant = Settings.ExtrusionMult * 4 * Settings.NozzleDiameter * Settings.Resolution.Z / (Math.PI * Math.Pow(1.75, 2));
            int layer = 0;
            ret.Add($";LAYER_COUNT:{lines.Count(x => x is NewLayerLine)}");
            bool prevTravel = lines.First().Travel;
            bool changePrintFeedRate = true;
            double curPrintFeedRate = wallFeedrate;
            foreach (Line line in lines)
            {
                if (line is NewLayerLine)
                {
                    ret.Add($";LAYER:{layer++}");
                }
                else if (line is WallLine)
                {
                    curPrintFeedRate = wallFeedrate;
                    changePrintFeedRate = true;
                    ret.Add(";TYPE:WALL-OUTER");
                }
                else if (line is InnerWallLine)
                {
                    curPrintFeedRate = innerWallFeedrate;
                    changePrintFeedRate = true;
                    ret.Add(";TYPE:WALL-INNER");
                }
                else if (line is SkinLine)
                {
                    curPrintFeedRate = innerWallFeedrate;
                    changePrintFeedRate = true;
                    ret.Add(";TYPE:SKIN");
                }
                else if (line is InfillLine)
                {
                    curPrintFeedRate = infillFeedrate;
                    changePrintFeedRate = true;
                    ret.Add(";TYPE:FILL");
                }
                else if (line is InfoLine)
                {
                    continue;
                }
                else if (line.Travel)
                {
                    string move = $"G0 X{N(line.End.X)} Y{N(line.End.Y)} Z{N(line.End.Z)}";
                    if (!prevTravel)
                    {
                        ret.Add($"G1 F{N(retractFeedrate)} E{N(extrusion - Settings.RetractionDistance)}");
                        move += $" F{N(travelFeedrate)}";
                        prevTravel = true;
                    }
                    ret.Add(move);
                }
                else
                {
                    if (prevTravel)
                    {
                        extrusion += Settings.RetractionDistance * 0.05f;
                        ret.Add($"G1 F{N(retractFeedrate)} E{N(extrusion)}");
                    }
                    extrusion += line.Flow * line.Length() * extrusionConstant;
                    string move = $"G1 X{N(line.End.X)} Y{N(line.End.Y)} Z{N(line.End.Z)} E{N(extrusion)}";
                    if (prevTravel || changePrintFeedRate)
                    {
                        move += $" F{N(curPrintFeedRate)}";
                        prevTravel = false;
                        changePrintFeedRate = false;
                    }
                    ret.Add(move);
                }
            }

            ret.Add("M140 S0");
            ret.Add("M104 S0");
            ret.Add("M107");
            ret.Add($"G1 F{N(retractFeedrate)} E{N(extrusion - 5)}");
            ret.Add($"G0 Z{N(Math.Min(Settings.PrintVolume.Z > 0 ? Settings.PrintVolume.Z : double.MaxValue, lines.Where(x => !(x is InfoLine)).Max(x => Math.Max(x.Start.Z, x.End.Z)) + 5))}");
            ret.Add($"G0 X0 Y{N(Math.Max(0, Settings.PrintVolume.Y - 5))}");
            ret.Add("M84");

            return ret;
        }
    }
}
