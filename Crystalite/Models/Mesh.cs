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
        public Matrix4x4 transform;

        public Vector3[] VBOData;
        public int vbo;
        public int vao;
        public OpenTK.Mathematics.Vector3 col;
        public bool updatedVBO = false;
        public bool hasVBOBound = false;

        public Mesh() { }

        public Mesh(IEnumerable<Triangle> triangles)
        {
            transform = Matrix4x4.CreateFromYawPitchRoll(0, 0, 0);
            foreach (Triangle triangle in triangles)
            {
                var a = Reorient(triangle.a);
                var b = Reorient(triangle.b);
                var c = Reorient(triangle.c);
                vertices.Add(a * 0.1f);
                vertices.Add(b * 0.1f);
                vertices.Add(c * 0.1f);

                normals.Add(Vector3.Normalize(Vector3.Cross(b - a, c - a)));
            }
            CalculateVAOData();
        }
        private Vector3 Reorient(Vector3 originalPos)
        {
            return new Vector3(originalPos.X, originalPos.Z, -originalPos.Y);
        }

        public void CalculateVAOData()
        {
            var vboList = new List<Vector3>();

            for (int i = 0; i < vertices.Count; i++)
            {
                vboList.Add(vertices[i]);
                vboList.Add(normals[i / 3]);
            }
            VBOData = vboList.ToArray();
            updatedVBO = true;
        }
    }
}
