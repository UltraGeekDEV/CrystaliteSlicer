using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform.Interop;
using Avalonia.Threading;
using Crystalite.Models;
using Crystalite.Utils;
using Models;
using OpenTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Crystalite.Utils.OpenGLUtils;
using static Crystalite.Utils.Shaders;

namespace Crystalite
{

    public class OpenGLViewPort : OpenGlControlBase
    {
        private OpenTKBinding oGLContext;

        private int frameBufferObject;
        private int frameBufferTexture;
        private int renderBufferObject;

        private int outlineFBO;
        private int outlineFBT;
        private int outlineRBO;

        private int shadowMapFBO;
        private int shadowMapFBT;
        private int shadowMapWidth = 2048;
        private int shadowMapHeight = 2048;

        Mesh postProcess;

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
                return;;

            Debug.WriteLine("resized");
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

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outlineFBO);

            //Set texture info and resize it
            GL.BindTexture(TextureTarget.Texture2D, outlineFBT);
            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgb, (int)size.Width, (int)size.Height, 0, PixelFormat.Rgb, PixelType.UnsignedByte, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget2d.Texture2D, outlineFBT, 0);

            CheckError("FBT2 resize");

            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, outlineRBO);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.Depth24Stencil8, (int)size.Width, (int)size.Height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, outlineRBO);

            CheckError("RB2 resize");

            shaders[Utils.ShaderType.frameBuffer].Activate();

            GL.Uniform1(GL.GetUniformLocation(shaders[Utils.ShaderType.frameBuffer].program, "screenTexture"), 0);
            GL.Uniform1(GL.GetUniformLocation(shaders[Utils.ShaderType.frameBuffer].program, "outlineTexture"), 1);

            CheckError("Set texture");
            CameraData.instance.aspectRatio = (float)(size.Width/size.Height);
        }
        protected override unsafe void OnOpenGlInit(GlInterface aGl)
        {
            SetupOpenTK(aGl);
            Shaders.Setup();
            Slicer.Setup();
            CheckError("Early Check");
            GL.DebugMessageCallback((DebugSource source,DebugType type,int id,DebugSeverity severity,int length,IntPtr pMessage,IntPtr pUserParam) => 
{
                // In order to access the string pointed to by pMessage, you can use Marshal
                // class to copy its contents to a C# string without unsafe code. You can
                // also use the new function Marshal.PtrToStringUTF8 since .NET Core 1.1.
                string message = Marshal.PtrToStringAnsi(pMessage, length);

                // The rest of the function is up to you to implement, however a debug output
                // is always useful.
                Debug.WriteLine("[{0} source={1} type={2} id={3}] {4}", severity, source, type, id, message);

                // Potentially, you may want to throw from the function for certain severity
                // messages.
                if (type == DebugType.DebugTypeError)
                {
                    throw new Exception(message);
                }
            },0);

            frameBufferObject = GL.GenFramebuffer();
            frameBufferTexture = GL.GenTexture();
            renderBufferObject = GL.GenRenderbuffer();

            outlineFBO = GL.GenFramebuffer();
            outlineFBT = GL.GenTexture();
            outlineRBO = GL.GenRenderbuffer();

            shadowMapFBO = GL.GenFramebuffer();
            shadowMapFBT = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, shadowMapFBT);
            GL.TexImage2D(TextureTarget2d.Texture2D,0,TextureComponentCount.DepthComponent32f,shadowMapWidth,shadowMapHeight,0,PixelFormat.DepthComponent,PixelType.Float,0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor,new float[]{ 1.0f,1.0f,1.0f,1.0f});

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, shadowMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.DepthAttachment,TextureTarget2d.Texture2D,shadowMapFBT,0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            Debug.WriteLine(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer));
            CheckError("Create shadow Buffer");

            this.GetObservable(BoundsProperty).Subscribe((a)=> OpenGLUtils.QueueAction(()=>Resize(a)));

            postProcess = new Mesh(new List<Triangle>
            {
                new Triangle(new Vector3(-10.0f,0,-10.0f),new Vector3(-10.0f,0,10.0f),new Vector3(10.0f,0,-10.0f)),
                new Triangle(new Vector3(-10.0f,0f,10.0f),new Vector3(10.0f,0,10.0f),new Vector3(10.0f,0,-10.0f))
            },Utils.ShaderType.frameBuffer);
            //MeshData.instance.meshes.Add(postProcess);
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
            OpenGLUtils.ExecuteSyncQueue();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outlineFBO);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferObject);

            CheckError("Render Early1");
            GL.ClearColor(0.1f, 0.15f, 0.2f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            CheckError("Render Early");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, shadowMapFBO);
            GL.Viewport(0, 0, shadowMapWidth, shadowMapHeight);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            GL.ColorMask(false, false, false, false);

            OpenGLUtils.CheckError("Pre Shadow Render");
            foreach (var modelGroup in MeshData.instance.objectPass)
            {
                foreach (var mesh in modelGroup)
                {
                    if (mesh.shader == Utils.ShaderType.lit)
                    {
                        Shaders.shaders[Utils.ShaderType.shadow].Activate();
                        var sun = Sun.view * Sun.Projection;
                        aGL.UniformMatrix4fv(GL.GetUniformLocation(Shaders.shaders[Utils.ShaderType.shadow].program, "sun"), 1, false, &sun);

                        mesh.Draw(aGL, Utils.ShaderType.shadow);
                    }
                }
            }

            GL.ColorMask(true, true, true, true);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferObject);
            GL.Viewport(0, 0, (int)Bounds.Width, (int)Bounds.Height);
            GL.Enable(EnableCap.DepthTest);

            foreach (var layer in MeshData.instance.objectPass)
            {
                foreach (var mesh in layer)
                {
                    if (!mesh.depthTest)
                    {
                        GL.Disable(EnableCap.DepthTest);
                    }
                    if (mesh.shader == Utils.ShaderType.lit)
                    {
                        shaders[Utils.ShaderType.lit].Activate();
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, shadowMapFBT);

                        GL.Uniform1(GL.GetUniformLocation(shaders[Utils.ShaderType.lit].program, "shadowMap"), 0);
                        var sun = Sun.view * Sun.Projection;
                        aGL.UniformMatrix4fv(GL.GetUniformLocation(shaders[Utils.ShaderType.lit].program, "sunProjection"), 1,false,&sun);
                        GL.Uniform3(GL.GetUniformLocation(shaders[Utils.ShaderType.lit].program, "SunPos"),new OpenTK.Mathematics.Vector3(Sun.dir.X,Sun.dir.Y,Sun.dir.Z));
                    }
                        mesh.Draw(aGL);

                        if (mesh.hasOutline)
                        {
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outlineFBO);
                            GL.Disable(EnableCap.DepthTest);

                            mesh.Draw(aGL, Utils.ShaderType.outline);

                            GL.Enable(EnableCap.DepthTest);
                            GL.BindFramebuffer(FramebufferTarget.Framebuffer, frameBufferObject);
                        }

                    if (!mesh.depthTest)
                    {
                        GL.Enable(EnableCap.DepthTest);
                    }
                }
            }

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb);
            var postProcessShader = Shaders.shaders[Utils.ShaderType.frameBuffer];
            postProcessShader.Activate();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, frameBufferTexture);

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, outlineFBT);

            GL.Uniform1(GL.GetUniformLocation(postProcessShader.program, "width"), (float)Bounds.Width);
            GL.Uniform1(GL.GetUniformLocation(postProcessShader.program, "height"), (float)Bounds.Height);
            postProcess.vao.Bind();
            GL.DrawArrays(PrimitiveType.Triangles, 0, postProcess.vertices.Count * 6);

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            GL.Clear(ClearBufferMask.DepthBufferBit);
            foreach ( var meshGroup in MeshData.instance.UIPass)
            {
                foreach (var mesh in meshGroup)
                {
                    mesh.Draw(aGL);
                }
            }
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fb);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, frameBufferObject);
            GL.BlitFramebuffer(0, 0, (int)Width-1, (int)Height-1, 0, 0, (int)Width-1, (int)Height-1, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            CheckError("Blit");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb);
            postProcessShader = Shaders.shaders[Utils.ShaderType.antiAliasing];
            postProcessShader.Activate();
            GL.BindTexture(TextureTarget.Texture2D, frameBufferTexture);
            GL.Uniform1(GL.GetUniformLocation(postProcessShader.program, "width"), (float)Bounds.Width);
            GL.Uniform1(GL.GetUniformLocation(postProcessShader.program, "height"), (float)Bounds.Height);
            postProcess.vao.Bind();
            GL.DrawArrays(PrimitiveType.Triangles, 0, postProcess.vertices.Count * 6);
            CheckError("AA");
        }
        
    }
}