using OpenTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public static class OpenGLUtils
    {
        public const int positionLocation = 0;
        public const int normalLocation = 1;
        public static void CheckError(string name)
        {
            ErrorCode err;
            while ((err = GL.GetError()) != ErrorCode.NoError)
                Debug.WriteLine(name + ": " + err);
        }
    }
}
