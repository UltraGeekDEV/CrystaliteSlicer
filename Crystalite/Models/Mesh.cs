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
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public Vector3 lowerLeft;
        public Vector3 upperRight;
        public Matrix4x4 transform;
        public ShaderType shader;

        public VBO vbo;
        public VAO vao;
        public OpenTK.Mathematics.Vector3 col;

        public Mesh() { }

        public Mesh(IEnumerable<Triangle> triangles,ShaderType shader)
        {
            lowerLeft = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            upperRight = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            transform = Matrix4x4.CreateFromYawPitchRoll(0, 0, 0);
            foreach (Triangle triangle in triangles)
            {
                var a = Reorient(triangle.a);
                var b = Reorient(triangle.b);
                var c = Reorient(triangle.c);

                lowerLeft = Min(lowerLeft, Min(a, Min(b, c)));
                upperRight = Max(lowerLeft, Max(a, Max(b, c)));

                vertices.Add(a * 0.1f);
                vertices.Add(b * 0.1f);
                vertices.Add(c * 0.1f);

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
            vbo = new VBO(vboList.ToArray());
            vao.LinkVBO(vbo,0);
            vao.Unbind();
        }
    }
}
