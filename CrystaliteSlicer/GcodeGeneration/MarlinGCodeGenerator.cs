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
        public string GetGcode(IEnumerable<Line> path)
        {
            throw new NotImplementedException();
        }
    }
}
