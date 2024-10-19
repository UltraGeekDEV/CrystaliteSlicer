using Avalonia.OpenGL;
using Crystalite.Utils;
using Models;
using OpenTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Models
{
    public class Mesh
    {
        public List<Triangle> tris = new List<Triangle>();
        public Vector3 lowerLeft;
        public Vector3 upperRight;
        public Vector3 com;

        public Matrix4x4 translation;
        public Matrix4x4 rotation;
        public Matrix4x4 scale;
        public Vector3 rotationAngles;

        public Utils.ShaderType shader;
        public bool depthTest = true;
        public bool hasOutline = false;

        public VBO vbo;
        public VAO vao;
        public OpenTK.Mathematics.Vector3 col;

        public Mesh() { }

        public Mesh(IEnumerable<Triangle> triangles,Utils.ShaderType shader,float scale = 0.1f,bool reorient = true,bool center = false)
        {
            lowerLeft = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            upperRight = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            if (reorient)
            {
                this.tris = triangles.Select(x=>new Triangle(Reorient(x.a), Reorient(x.b), Reorient(x.c))).ToList();
            }
            else
            {
                this.tris = triangles.ToList();
            }

            translation = Matrix4x4.Identity;
            rotation = Matrix4x4.Identity;
            this.scale = Matrix4x4.CreateScale(scale);
            foreach (Triangle triangle in tris)
            {
                var a = triangle.a;
                var b = triangle.b;
                var c = triangle.c;
                com += a + b + c;
                lowerLeft = Min(lowerLeft, Min(a, Min(b, c)));
                upperRight = Max(upperRight, Max(a, Max(b, c)));
            }

            com /= tris.Count*3;
            if (center)
            {
                lowerLeft = Vector3.Transform(lowerLeft, this.scale) - Vector3.Transform(com, this.scale);
                upperRight = Vector3.Transform(upperRight, this.scale) - Vector3.Transform(com, this.scale);

                tris = tris.Select(x => { x.Offset(-com); return x; }).ToList();
            }
            else
            {
                lowerLeft = Vector3.Transform(lowerLeft, this.scale);
                upperRight = Vector3.Transform(upperRight, this.scale);
            }

            CalculateVAOData();
            this.shader = shader;
        }
        private Vector3 Min(Vector3 a, Vector3 b)
        {
            return new Vector3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        }
        private Vector3 Max(Vector3 a, Vector3 b)
        {
            return new Vector3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        }
        public static Vector3 Reorient(Vector3 originalPos)
        {
            return new Vector3(originalPos.X, originalPos.Z, -originalPos.Y);
        }
        public static Vector3 Deorient(Vector3 originalPos)
        {
            return new Vector3(originalPos.X, -originalPos.Z, originalPos.Y);
        }
        public void CalculateVAOData()
        {
            var vboList = new List<float>();

            for (int i = 0; i < tris.Count; i++)
            {
                var tri = tris[i];
                var normal = Vector3.Normalize(Vector3.Cross(tri.b - tri.a, tri.c - tri.a));

                vboList.Add(tri.a.X);
                vboList.Add(tri.a.Y);
                vboList.Add(tri.a.Z);

                vboList.Add(normal.X);
                vboList.Add(normal.Y);
                vboList.Add(normal.Z);

                vboList.Add(tri.b.X);
                vboList.Add(tri.b.Y);
                vboList.Add(tri.b.Z);

                vboList.Add(normal.X);
                vboList.Add(normal.Y);
                vboList.Add(normal.Z);

                vboList.Add(tri.c.X);
                vboList.Add(tri.c.Y);
                vboList.Add(tri.c.Z);

                vboList.Add(normal.X);
                vboList.Add(normal.Y);
                vboList.Add(normal.Z);
            }
            vao = new VAO();
            
            vao.Bind();
            OpenGLUtils.CheckError("Vao create");
            vbo = new VBO(vboList.ToArray());
            vao.LinkVBO(vbo,0);
            OpenGLUtils.CheckError("Vao link");

            vao.Unbind();
        }

        public (float,Mesh) RayCast(Vector3 dir,Vector3 start)
        {
            var hits = GetWorldspacetriangles()
                .Select(x =>
            {
                var norm = Vector3.Normalize(Vector3.Cross(x.b - x.a, x.c - x.a));
                var d = Vector3.Dot(x.a,norm);

                var dirDot = Vector3.Dot(norm, dir);

                if(dirDot < 0.0001f)
                {
                    return -1.0f;
                }

                var t = (d - Vector3.Dot(norm, start)) / dirDot;

                var q = start + t * dir;

                var abCheck = Vector3.Dot(Vector3.Cross(x.b - x.a, q - x.a),norm);

                if (abCheck < 0)
                {
                    return -1.0f;
                }

                var acCheck = Vector3.Dot(Vector3.Cross(x.a - x.c, q - x.c), norm);

                if (acCheck < 0)
                {
                    return -1.0f;
                }

                var bcCheck = Vector3.Dot(Vector3.Cross(x.c - x.b, q - x.b), norm);

                if (bcCheck < 0)
                {
                    return -1.0f;
                }

                return t;
            })
                .Where(x=>x >= 0);

            if (hits.Count() > 0)
            {
                return (hits.Min(), this);
            }
            else
            {
                return (-1.0f, this);
            }
        }

        public unsafe void Draw(GlInterface aGL)
        {
            var Meshshader = Shaders.shaders[shader];
            Meshshader.Activate();
            vao.Bind();

            var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, CameraData.instance.aspectRatio, 0.01f, 1000f);
            var view = CameraData.CreateViewMatrix();

            var viewLoc = GL.GetUniformLocation(Meshshader.program, "view");
            var modelLoc = GL.GetUniformLocation(Meshshader.program, "translation");
            var projectionLoc = GL.GetUniformLocation(Meshshader.program, "projection");
            var col = GL.GetUniformLocation(Meshshader.program, "col");
            var scale = GL.GetUniformLocation(Meshshader.program, "scale");
            var rotation = GL.GetUniformLocation(Meshshader.program, "rotation");

            OpenGLUtils.CheckError("Render Get locs");
            var locMat = this.translation;
            var scaleMat = this.scale;
            var rotMat = this.rotation;

            aGL.UniformMatrix4fv(viewLoc, 1, false, &view);
            aGL.UniformMatrix4fv(projectionLoc, 1, false, &projection);
            aGL.UniformMatrix4fv(modelLoc, 1, false, &locMat);
            aGL.UniformMatrix4fv(scale, 1, false, &scaleMat);
            aGL.UniformMatrix4fv(rotation, 1, false, &rotMat);
            OpenGLUtils.CheckError("Render Set Attribs");

            GL.Uniform3(col, ref this.col);
            GL.DrawArrays(PrimitiveType.Triangles, 0, tris.Count*3);
            OpenGLUtils.CheckError("Render");
            vao.Unbind();
        }
        public void Draw(GlInterface aGL,Utils.ShaderType shader)
        {
            var originalShader = this.shader;
            this.shader = shader;
            Draw(aGL);
            this.shader = originalShader;
        }
        public void Draw(GlInterface aGL, Utils.ShaderType shader,OpenTK.Mathematics.Vector3 col)
        {
            var originalColor = this.col;
            this.col = col;
            Draw(aGL,shader);
            this.col = originalColor;
        }
        public List<Triangle> GetWorldspacetriangles()
        {
            return tris.Select(x => new Triangle(
                Vector3.Transform(Vector3.Transform(Vector3.Transform(x.a,rotation),scale), translation),
                Vector3.Transform(Vector3.Transform(Vector3.Transform(x.b, rotation), scale), translation),
                Vector3.Transform(Vector3.Transform(Vector3.Transform(x.c, rotation), scale), translation)
                )).ToList();
        }
        public List<Triangle> GetPrintspacetriangles()
        {
            Matrix4x4 inverseScale ;
            Matrix4x4.Invert(scale,out inverseScale);
            return tris.Select(x => new Triangle(
                Vector3.Transform(Vector3.Transform(Vector3.Transform(Vector3.Transform(x.a, rotation), scale), translation),inverseScale),
                Vector3.Transform(Vector3.Transform(Vector3.Transform(Vector3.Transform(x.b, rotation), scale), translation),inverseScale),
                Vector3.Transform(Vector3.Transform(Vector3.Transform(Vector3.Transform(x.c, rotation), scale), translation),inverseScale)
                )).ToList();
        }

        internal void CreateFromYawPitchRoll()
        {
            var quat = Quaternion.CreateFromYawPitchRoll(rotationAngles.Y, rotationAngles.X, rotationAngles.Z);
            rotation = Matrix4x4.CreateFromQuaternion(quat);
        }

        public void Rotate(Vector3 rot)
        {
            rotation = rotation * Matrix4x4.CreateRotationX(rot.X) * Matrix4x4.CreateRotationY(rot.Y) * Matrix4x4.CreateRotationZ(rot.Z);
        }
    }
}
