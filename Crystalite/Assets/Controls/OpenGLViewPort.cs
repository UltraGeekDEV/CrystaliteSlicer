using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
using System.Threading.Tasks;
using static Crystalite.Utils.OpenGLUtils;

namespace Crystalite
{

    public class OpenGLViewPort : OpenGlControlBase
    {
        private OpenTKBinding oGLContext;

        private int frameBufferObject;
        private int frameBufferTexture;
        private int renderBufferObject;
        private int fboVBO, fboVAO;
        private float[] fboTri =
        {
            1.0f,-1.0f, 1.0f,0.0f,
            -1.0f,-1.0f,0.0f,0.0f,
            -1.0f,1.0f,0.0f,1.0f,

            1.0f,1.0f,1.0f,1.0f,
            1.0f,-1.0f,1.0f,0.0f,
            -1.0f,1.0f,0.0f,1.0f
        };

        private void SetupOpenTK(GlInterface aGLContext)
        {
            oGLContext = new OpenTKBinding(aGLContext);
            GL.LoadBindings(oGLContext);
        }

        private void Resize(Rect size)
        {
            if (double.IsNaN(size.Width))
                return;

            if (double.IsNaN(size.Height))
                return;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferObject);

            //Set texture info and resize it
            GL.BindTexture(TextureTarget.Texture2D, frameBufferTexture);
            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgb, (int)size.Width, (int)size.Height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.ColorAttachment0,TextureTarget2d.Texture2D,frameBufferTexture,0);

            CheckError("FBT resize");

            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderBufferObject);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.Depth24Stencil8, (int)size.Width, (int)size.Height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, renderBufferObject);
            
            CheckError("RB resize");
        }
        protected override unsafe void OnOpenGlInit(GlInterface aGl)
        {
            SetupOpenTK(aGl);
            Shaders.Setup();
            Slicer.Setup();
            CheckError("Early Check");

            frameBufferObject = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferObject);

            frameBufferTexture = GL.GenTexture();

            renderBufferObject = GL.GenRenderbuffer();

            this.GetObservable(BoundsProperty).Subscribe(Resize);

            fboVAO = GL.GenVertexArray();
            fboVBO = GL.GenBuffer();
            GL.BindVertexArray(fboVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, fboVBO);
            fixed (float* fboQuad = fboTri)
                GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * fboTri.Length, (nint)fboQuad, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float,false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float,false, 4 * sizeof(float), 2*sizeof(float));

            CheckError("Setup fbo quad");
        }

        protected override void OnOpenGlDeinit(GlInterface GL)
        {
            foreach (var item in Shaders.shaders.Values)
            {
                item.Delete();
            }
        }

        protected override unsafe void OnOpenGlRender(GlInterface aGL, int fb)
        {
            Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);

            GL.ClearColor(0.091f, 0.09f, 0.1f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            GL.CullFace(CullFaceMode.Back);
            GL.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);

            CheckError("Render Early");

            foreach (var mesh in MeshData.instance.meshes)
            {
                var shader = Shaders.shaders[mesh.shader];
                shader.Activate();

                var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)(Bounds.Width / Bounds.Height), 0.01f, 1000f);
                var view = CameraData.CreateViewMatrix();
                var viewLoc = GL.GetUniformLocation(shader.program, "view");
                var modelLoc = GL.GetUniformLocation(shader.program, "model");
                var projectionLoc = GL.GetUniformLocation(shader.program, "projection");
                var col = GL.GetUniformLocation(shader.program, "col");
                CheckError("Render Get locs");

                aGL.UniformMatrix4fv(viewLoc, 1, false, &view);
                aGL.UniformMatrix4fv(projectionLoc, 1, false, &projection);
                CheckError("Render Set Attribs");

                GL.Uniform3(col, ref mesh.col);
                fixed (Matrix4x4* model = &mesh.transform)
                {
                    aGL.UniformMatrix4fv(modelLoc, 1, false, model);
                }
                mesh.vao.Bind();
                GL.DrawArrays(PrimitiveType.Triangles, 0, mesh.vertices.Count/3);
                CheckError("Render");
            }

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }
        
    }
}