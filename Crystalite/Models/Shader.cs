using OpenTK.Graphics.ES30;
using System;
using System.IO;
using static Crystalite.Utils.OpenGLUtils;

namespace Crystalite.Models
{
    public class Shader
    {
        public int frag;
        public int vert;
        public int program;

        public Shader(string fragFile, string vertFile)
        {
            frag = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(frag, GetShader(fragFile));
            GL.CompileShader(frag);
            CheckError("Compile Fragment");

            vert = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vert, GetShader(vertFile));
            GL.CompileShader(vert);
            CheckError("Compile Vertex");

            program = GL.CreateProgram();
            GL.AttachShader(program, vert);
            GL.AttachShader(program, frag);
            CheckError("Attach Shaders");

            GL.LinkProgram(program);
            CheckError("Link Program");
        }
        public void Activate()
        {
            GL.UseProgram(program);
        }
        public void Delete()
        {
            GL.DeleteProgram(program);
        }
        private string GetShader(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", name));
        }
    }
}
