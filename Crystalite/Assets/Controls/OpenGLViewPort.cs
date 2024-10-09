using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform.Interop;
using Avalonia.Threading;
using Crystalite.Utils;
using OpenTK.Graphics.ES30;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

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
        private OpenTKBinding oGLContext;

        const int positionLocation = 0;
        const int normalLocation = 1;

        [StructLayout(LayoutKind.Sequential,Pack = 4)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        private void SetupOpenTK(GlInterface aGLContext)
        {
            oGLContext = new OpenTKBinding(aGLContext);
            GL.LoadBindings(oGLContext);
        }

        private static void CheckError(string name)
        {
            ErrorCode err;
            while ((err = GL.GetError()) != ErrorCode.NoError)
                Debug.WriteLine(name +": "+err);
        }

        protected override unsafe void OnOpenGlInit(GlInterface aGl)
        {
            SetupOpenTK(aGl);
            CheckError("Early Check");

            vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, GetShader("ViewPort.vert"));
            GL.CompileShader(vertexShader);
            CheckError("Compile Vertex");


            fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, GetShader("ViewPort.frag"));
            GL.CompileShader(fragmentShader);
            CheckError("Compile Fragment");

            shaderProgram = GL.CreateProgram();
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);
            CheckError("Attach Shaders");

            GL.BindAttribLocation(shaderProgram, positionLocation, "aPos");
            GL.BindAttribLocation(shaderProgram, normalLocation, "aNormal");
            CheckError("Bind Attrib");


            GL.LinkProgram(shaderProgram);
            CheckError("Link Program");
            SetVBOData();
            CheckError("Bind Buffer");

            vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(vertexBufferObject);
            CheckError( "Bind Array");

            CheckError("Enable Vertex Attrib");
        }

        private unsafe void SetVBOData()
        {
            foreach (var mesh in MeshData.instance.meshes)
            {
                if (mesh.updatedVBO)
                {
                    if (!mesh.hasVBOBound)
                    {
                        mesh.vbo = GL.GenBuffer();
                        mesh.vao = GL.GenVertexArray();
                        mesh.hasVBOBound = true;
                    }

                    fixed (void* vertexData = mesh.VBOData)
                    {
                        GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.vbo);
                        GL.BufferData(BufferTarget.ArrayBuffer, sizeof(Vector3) * mesh.VBOData.Length, (IntPtr)vertexData, BufferUsageHint.StaticDraw);
                        GL.BindVertexArray(mesh.vao);

                        GL.EnableVertexAttribArray(0);
                        GL.VertexAttribPointer(positionLocation, 3, VertexAttribPointerType.Float, false, 2 * sizeof(Vector3), IntPtr.Zero);
                        GL.VertexAttribPointer(normalLocation, 3, VertexAttribPointerType.Float, false, 2 * sizeof(Vector3), new IntPtr(12));
                        GL.EnableVertexAttribArray(positionLocation);
                        GL.EnableVertexAttribArray(normalLocation);

                        mesh.updatedVBO = false;
                    }
                }
            }
            GL.BindVertexArray(0);
        }

        protected override void OnOpenGlDeinit(GlInterface GL)
        {
            GL.DeleteProgram(shaderProgram);
            GL.DeleteShader(fragmentShader);
            GL.DeleteShader(vertexShader);
        }

        protected override unsafe void OnOpenGlRender(GlInterface aGL, int fb)
        {
            Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);

            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            GL.CullFace(CullFaceMode.Back);
            GL.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);

            GL.UseProgram(shaderProgram);

            SetVBOData();


            var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)(Bounds.Width / Bounds.Height), 0.01f, 1000f);
            var view = CameraData.CreateViewMatrix();
            var viewLoc = GL.GetUniformLocation(shaderProgram, "view");
            var modelLoc = GL.GetUniformLocation(shaderProgram, "model");
            var projectionLoc = GL.GetUniformLocation(shaderProgram, "projection");
            var col = GL.GetUniformLocation(shaderProgram, "col");
            CheckError("Render Get locs");

            aGL.UniformMatrix4fv(viewLoc, 1, false, &view);
            aGL.UniformMatrix4fv(projectionLoc, 1, false, &projection);
            CheckError("Render Set Attribs");

            CheckError("Render Early");

            foreach (var mesh in MeshData.instance.meshes)
            {
                GL.Uniform3(col, ref mesh.col);
                fixed (Matrix4x4* model = &mesh.transform)
                {
                    aGL.UniformMatrix4fv(modelLoc, 1, false, model);
                }
                GL.BindVertexArray(mesh.vao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, mesh.VBOData.Length);
                CheckError("Render");
            }

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }
        private string GetShader(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", name));
        }
    }
}