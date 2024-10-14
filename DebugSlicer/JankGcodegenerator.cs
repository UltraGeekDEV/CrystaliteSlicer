using Models;
using Models.GcodeInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugSlicer
{
    internal class JankGcodegenerator
    {
        public static List<string> GetGCode(List<Line> toolpath)
        {
            List<string> ret = new List<string>();

            var nfi = new NumberFormatInfo();
            nfi.NumberDecimalDigits = '.';
            nfi.NumberDecimalDigits = 4;

            float wallFeedrate = Settings.OuterWallSpeed * 60;
            float innerWallFeedrate = Settings.InnerWallSpeed * 60;
            float infillFeedrate = Settings.InfillSpeed * 60;
            float travelFeedrate = Settings.TravelSpeed * 60;
            float retractFeedrate = Settings.RetractionSpeed * 60;

            ret.Add($"M140 S{Settings.BedTemp}");
            ret.Add($"M105");
            ret.Add($"M190 S{Settings.BedTemp}");
            ret.Add($"M104 S{Settings.HotendTemp}");
            ret.Add($"M105");
            ret.Add($"M109 S{Settings.HotendTemp}");
            ret.Add($"M106 S{(int)(Settings.FanSpeed*255)}");

            ret.Add("M82"); //absolute extrusion
            ret.Add("G92 E0"); //reset extruder
            ret.Add("G28"); //home

            ret.Add("BED_MESH_PROFILE load=default"); //##########REMOVE#################


            ret.Add($"G0 F{travelFeedrate}");

            var startPos = toolpath.First(x=>!(x is InfoLine)).Start;
            ret.Add($"G0 X{startPos.X.ToString(nfi)} Y{startPos.Y.ToString(nfi)} Z{startPos.Z.ToString(nfi)}");

            double extrusion = 0;
            double extrusionConstant = 1.1f*4*Settings.NozzleDiameter * Settings.Resolution.Z / (Math.PI * Math.Pow(1.75, 2));
            int layer = 0;
            ret.Add($";LAYER_COUNT:{toolpath.Count(x=>x is NewLayerLine)}");
            ret.Add($";LAYER:{layer++}");
            bool prevTravel = toolpath.First().Travel;
            bool changePrintFeedRate = true;
            float curPrintFeedRate = wallFeedrate;
            foreach (Line line in toolpath)
            {
                if (line is NewLayerLine)
                {
                    ret.Add($";LAYER:{layer++}");
                    ret.Add($"TIMELAPSE_TAKE_FRAME");//##########REMOVE#################
                }
                else if(line is WallLine)
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
                else if (line is InfillLine)
                {
                    curPrintFeedRate = infillFeedrate;
                    changePrintFeedRate = true;
                    ret.Add(";TYPE:FILL");
                }
                else if (line.Travel)
                {
                    string move = $"G0 X{line.End.X.ToString(nfi)} Y{line.End.Y.ToString(nfi)} Z{line.End.Z.ToString(nfi)}";
                    if (!prevTravel)
                    {
                        ret.Add($"G1 F{retractFeedrate.ToString(nfi)} E{(extrusion-Settings.RetractionDistance).ToString(nfi)}");
                        move += $" F{travelFeedrate}";
                        prevTravel = true;
                    }
                    ret.Add(move);
                }
                else
                {
                    if (prevTravel)
                    {
                        extrusion += Settings.RetractionDistance * 0.05f;
                        ret.Add($"G1 F{retractFeedrate.ToString(nfi)} E{(extrusion).ToString(nfi)}");
                    }
                    extrusion += line.Flow * line.Length() * extrusionConstant;
                    string move = $"G1 X{line.End.X.ToString(nfi)} Y{line.End.Y.ToString(nfi)} Z{line.End.Z.ToString(nfi)} E{extrusion.ToString(nfi)}";
                    if (prevTravel || changePrintFeedRate)
                    {
                        move += $" F{curPrintFeedRate}";
                        prevTravel = false;
                        changePrintFeedRate = false;
                    }
                    ret.Add(move);
                }
            }

            ret.Add($"M190 S{0}");
            ret.Add($"M109 S{0}");
            ret.Add($"M107");
            ret.Add($"G1 F{retractFeedrate.ToString(nfi)} E{(extrusion-5).ToString(nfi)}");
            ret.Add($"G0 Z{(toolpath.Where(x=>!(x is InfoLine)).Max(x=> Math.Max(x.Start.Z,x.End.Z))+5).ToString(nfi)}");
            ret.Add($"G0 X0 Y{(Settings.PrintVolume.Y-5).ToString(nfi)}");

            return ret;
        }
    }
}
