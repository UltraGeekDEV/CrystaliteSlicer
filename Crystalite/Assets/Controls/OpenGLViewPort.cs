using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform.Interop;
using Avalonia.Threading;
using Crystalite.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using static Avalonia.OpenGL.GlConsts;

namespace Crystalite
{

    public class OpenGLViewPort : OpenGlControlBase
    {
        private int vertexShader;
        private int fragmentShader;
        private int shaderProgram;
        private int vertexBufferObject;
        private int indexBufferObject;
        private int vertexArrayObject;

        [StructLayout(LayoutKind.Sequential,Pack = 4)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        private static void CheckError(GlInterface gl, string name)
        {
            int err;
            while ((err = gl.GetError()) != GL_NO_ERROR)
                Debug.WriteLine(name +": "+err);
        }

        protected override unsafe void OnOpenGlInit(GlInterface GL)
        {
            CheckError(GL,"Early Check");

            vertexShader = GL.CreateShader(GL_VERTEX_SHADER);
            Debug.WriteLine(GL.CompileShaderAndGetError(vertexShader, GetShader("ViewPort.vert")));
            CheckError(GL, "Compile Vertex");


            fragmentShader = GL.CreateShader(GL_FRAGMENT_SHADER);
            Debug.WriteLine( GL.CompileShaderAndGetError(fragmentShader, GetShader("ViewPort.frag")));
            CheckError(GL, "Compile Fragment");

            shaderProgram = GL.CreateProgram();
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);
            CheckError(GL, "Attach Shaders");

            const int positionLocation = 0;
            const int normalLocation = 1;
            GL.BindAttribLocationString(shaderProgram, positionLocation, "aPos");
            //GL.BindAttribLocationString(shaderProgram, normalLocation, "aNormal");
            CheckError(GL, "Bind Attrib");


            Debug.WriteLine(GL.LinkProgramAndGetError(shaderProgram));
            CheckError(GL, "Link Program");

            vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(GL_ARRAY_BUFFER, vertexBufferObject);
            fixed (float* vertexData = vertices)
            {
                GL.BufferData(GL_ARRAY_BUFFER, sizeof(float) * vertices.Length, (IntPtr)vertexData, GL_STATIC_DRAW);
            }

            CheckError(GL, "Bind Buffer");

            vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(vertexArrayObject);
            CheckError(GL, "Bind Array");

            GL.VertexAttribPointer(positionLocation, 3, GL_FLOAT, 0, 3 * sizeof(float), IntPtr.Zero);
            GL.EnableVertexAttribArray(positionLocation);
            CheckError(GL, "Enable Vertex Attrib");
        }
        private readonly float[] vertices =
        {
            -1.0f,-1.0f,0.0f,
            1.0f,-1.0f,0.0f,
            -1.0f,1.0f,0.0f,

            1.0f,-1.0f,0.0f,
            1.0f, 1.0f, 0.0f,
            -1.0f, 1.0f, 0.0f
        };
        protected override void OnOpenGlDeinit(GlInterface GL)
        {
            GL.DeleteProgram(shaderProgram);
            GL.DeleteShader(fragmentShader);
            GL.DeleteShader(vertexShader);
        }

        protected override unsafe void OnOpenGlRender(GlInterface GL, int fb)
        {
            Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            GL.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);

            GL.UseProgram(shaderProgram);
            GL.BindVertexArray(vertexArrayObject);

            CheckError(GL, "Render Early");

            var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/4 , (float)(Bounds.Width / Bounds.Height), 0.01f, 1000f);
            var view = CameraData.CreateViewMatrix();
            var model = Matrix4x4.CreateFromYawPitchRoll(0, 0, 0);
            var viewLoc = GL.GetUniformLocationString(shaderProgram,"view");
            var modelLoc = GL.GetUniformLocationString(shaderProgram,"model");
            var projectionLoc = GL.GetUniformLocationString(shaderProgram,"projection");
            CheckError(GL, "Render Get locs");

            GL.UniformMatrix4fv(viewLoc, 1, false, &view);
            GL.UniformMatrix4fv (projectionLoc, 1, false, &projection);
            GL.UniformMatrix4fv(modelLoc,1, false, &model);
            CheckError(GL, "Render Set Attribs");

            GL.DrawArrays(GL_TRIANGLES, 0, 6);
            CheckError(GL, "Render");

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }
        private string GetShader(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", name));
        }
    }
}