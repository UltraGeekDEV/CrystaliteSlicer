using Avalonia.Animation;
using OpenTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Models
{
    public unsafe class VBO
    {
        public int id;
        public VBO(float[] data)
        {
            id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, id);
            fixed(float* vertexData = data)
            {
                GL.BufferData(BufferTarget.ArrayBuffer,data.Length*sizeof(float),(nint)vertexData,BufferUsageHint.StaticDraw);
            }
        }
        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, id);
        }

        public void UnBind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        }

        public void Delete()
        {
            GL.DeleteBuffer(id);
        }
    }
}
