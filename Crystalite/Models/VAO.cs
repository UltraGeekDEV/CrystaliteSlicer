using OpenTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Models
{
    public unsafe class VAO
    {
        int id;
        public VAO() 
        {
            id = GL.GenVertexArray();
        }

        public void LinkVBO(VBO vbo,int layout)
        {
            vbo.Bind();
            GL.VertexAttribPointer(0,3,VertexAttribPointerType.Float,false,6*sizeof(float),0);
            GL.VertexAttribPointer(1,3,VertexAttribPointerType.Float,false,6*sizeof(float),3*sizeof(float));
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            vbo.UnBind();
        }
        public void Bind()
        {
            GL.BindVertexArray(id);
        }
        public void Unbind()
        {
            GL.BindVertexArray(0);
        }
        public void Delete()
        {
            GL.DeleteBuffer(id);
        }
    }
}
