using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DebugSlicer
{
    public static class TriangleExtentions
    {
        public static byte[] ToSTLFacet(this Triangle tri)
        {
            byte[] ret = new byte[50];

            Vector3 normal = Vector3.Normalize(Vector3.Cross(tri.c - tri.a, tri.b - tri.a));

            Buffer.BlockCopy(BitConverter.GetBytes(normal.X), 0, ret, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(normal.Y), 0, ret, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(normal.Z), 0, ret, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.a.X), 0, ret, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.a.Y), 0, ret, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.a.Z), 0, ret, 20, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.b.X), 0, ret, 24, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.b.Y), 0, ret, 28, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.b.Z), 0, ret, 32, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.c.X), 0, ret, 36, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.c.Y), 0, ret, 40, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(tri.c.Z), 0, ret, 44, 4);
            ret[48] = 0;
            ret[49] = 0;

            return ret;
        }
    }

    public class STLHelper
    {
        public static void Export(IEnumerable<Triangle> mesh, string path)
        {
            byte[] data = new byte[mesh.Count() * 50 + 84];

            Buffer.BlockCopy(BitConverter.GetBytes(mesh.Count()), 0, data, 80, 4);
            int curIndex = 84;
            foreach (Triangle triangle in mesh)
            {
                Buffer.BlockCopy(triangle.ToSTLFacet(), 0, data, curIndex, 50);
                curIndex += 50;
            }

            File.WriteAllBytes(path, data);
        }
    }
}
