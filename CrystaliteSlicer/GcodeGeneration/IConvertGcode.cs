using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.GcodeGeneration
{
    public interface IConvertGcode
    {
        public string GetGcode(IEnumerable<Line> path);
    }
}
