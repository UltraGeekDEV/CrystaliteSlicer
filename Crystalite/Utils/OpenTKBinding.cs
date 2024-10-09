using Avalonia.OpenGL;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public class OpenTKBinding : IBindingsContext
    {
        private GlInterface aGLInterface;

        public OpenTKBinding(GlInterface glInterface)
        {
            this.aGLInterface = glInterface;
        }

        public nint GetProcAddress(string procName)
        {
            return aGLInterface.GetProcAddress(procName);
        }
    }
}
