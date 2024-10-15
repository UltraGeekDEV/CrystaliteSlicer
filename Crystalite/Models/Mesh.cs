using Crystalite.Utils;
using Models;
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
        public IEnumerable<Triangle> tris = new List<Triangle>();
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public Vector3 lowerLeft;
        public Vector3 upperRight;
        public Matrix4x4 transform;
        public ShaderType shader;
        public bool clickable = true;
        public bool hasOutline = false;

        public VBO vbo;
        public VAO vao;
        public OpenTK.Mathematics.Vector3 col;

        public Mesh() { }

        public Mesh(IEnumerable<Triangle> triangles,ShaderType shader,float scale = 0.1f,bool reorient = true)
        {
            lowerLeft = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            upperRight = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            if (reorient)
            {
                this.tris = triangles.Select(x=>new Triangle(Reorient(x.a)*scale, Reorient(x.b) * scale, Reorient(x.c) * scale));
            }
            else
            {
                this.tris = triangles;
            }

            transform = Matrix4x4.Identity;
            foreach (Triangle triangle in tris)
            {
                var a = triangle.a;
                var b = triangle.b;
                var c = triangle.c;

                lowerLeft = Min(lowerLeft, Min(a, Min(b, c)));
                upperRight = Max(lowerLeft, Max(a, Max(b, c)));

                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);

                normals.Add(Vector3.Normalize(Vector3.Cross(b - a, c - a)));
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
        private Vector3 Reorient(Vector3 originalPos)
        {
            return new Vector3(originalPos.X, originalPos.Z, -originalPos.Y);
        }

        public void CalculateVAOData()
        {
            var vboList = new List<float>();

            for (int i = 0; i < vertices.Count; i++)
            {
                vboList.Add(vertices[i].X);
                vboList.Add(vertices[i].Y);
                vboList.Add(vertices[i].Z);
                vboList.Add(normals[i / 3].X);
                vboList.Add(normals[i / 3].Y);
                vboList.Add(normals[i / 3].Z);
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
            if (!clickable)
            {
                return (-1.0f, this);
            }
            var hits = tris.Select(x=>new Triangle(Vector3.Transform(x.a,transform), Vector3.Transform(x.b, transform), Vector3.Transform(x.c, transform)))
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
    }
}
