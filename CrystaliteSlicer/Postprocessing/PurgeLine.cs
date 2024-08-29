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
    public class PurgeLine : IPostprocessToolpath
    {
        public IEnumerable<Line> Process(IEnumerable<Line> path)
        {
            float lineMaterial = Settings.MaxLayerHeight / Settings.Resolution.Z * 2;

            Vector3 a = new Vector3(5, 10, Settings.MaxLayerHeight*0.25f);
            Vector3 b = new Vector3(5, Settings.PrintVolume.Y-10, Settings.MaxLayerHeight * 0.25f);

            return new List<Line>() { 
                new Line(a+new Vector3(0,0, Settings.MaxLayerHeight),b,lineMaterial,false),
                new Line(b,b+new Vector3(Settings.NozzleDiameter,0,0), 0,true),
                new Line(b+new Vector3(Settings.NozzleDiameter,0,0),a+new Vector3(Settings.NozzleDiameter,0,0), lineMaterial,false),
                new Line(a+new Vector3(Settings.NozzleDiameter,0,0),a+new Vector3(Settings.NozzleDiameter,0,1), 0,true),
                new Line(a+new Vector3(Settings.NozzleDiameter,0,1),path.First(x=>!(x is InfoLine)).Start, 0,true),
            }.Concat(path);
        }
    }
}
