using Models;
using System.Numerics;

namespace CrystaliteSlicer.Tests
{
    /// <summary>Closed, outward-wound test meshes.</summary>
    public static class TestMeshes
    {
        public static List<Triangle> Box(Vector3 min, Vector3 max, bool inverted = false)
        {
            var v = new[]
            {
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
                new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
            };
            int[][] faces =
            {
                new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, // bottom
                new[] { 4, 5, 6 }, new[] { 4, 6, 7 }, // top
                new[] { 0, 1, 5 }, new[] { 0, 5, 4 },
                new[] { 1, 2, 6 }, new[] { 1, 6, 5 },
                new[] { 2, 3, 7 }, new[] { 2, 7, 6 },
                new[] { 3, 0, 4 }, new[] { 3, 4, 7 },
            };
            return faces.Select(f => inverted
                ? new Triangle(v[f[0]], v[f[2]], v[f[1]])
                : new Triangle(v[f[0]], v[f[1]], v[f[2]])).ToList();
        }

        public static List<Triangle> Cylinder(Vector2 center, float radius, float z0, float z1, int segments = 256)
        {
            var tris = new List<Triangle>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathF.Tau * i / segments, a1 = MathF.Tau * (i + 1) / segments;
                var p0 = new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius + center;
                var p1 = new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius + center;
                var b0 = new Vector3(p0, z0); var b1 = new Vector3(p1, z0);
                var t0 = new Vector3(p0, z1); var t1 = new Vector3(p1, z1);
                var cb = new Vector3(center, z0); var ct = new Vector3(center, z1);
                tris.Add(new Triangle(cb, b1, b0));
                tris.Add(new Triangle(ct, t0, t1));
                tris.Add(new Triangle(b0, b1, t1));
                tris.Add(new Triangle(b0, t1, t0));
            }
            return tris;
        }

        /// <summary>Pillar with a thick horizontal shelf sticking out at the top: a 90° overhang.</summary>
        public static List<Triangle> TShape()
        {
            var tris = Box(new Vector3(40, 40, 0), new Vector3(50, 50, 12));
            tris.AddRange(Box(new Vector3(40, 40, 8), new Vector3(58, 50, 12)));
            return tris;
        }

        /// <summary>
        /// A tall hub with a floating rim reachable only through one spoke: the hub must not outgrow the rim's
        /// clearance while the rim is still waiting for support (the 150-tooth gear failure).
        /// </summary>
        public static List<Triangle> HubSpokeRim()
        {
            var tris = Box(new Vector3(45, 45, 0), new Vector3(55, 55, 12));
            tris.AddRange(Box(new Vector3(54, 48, 5), new Vector3(76, 52, 8)));
            tris.AddRange(Box(new Vector3(75, 30, 5), new Vector3(80, 70, 8)));
            return tris;
        }

        public static List<Triangle> HollowBox()
        {
            var tris = Box(new Vector3(40, 40, 0), new Vector3(60, 60, 20));
            tris.AddRange(Box(new Vector3(45, 45, 5), new Vector3(55, 55, 15), inverted: true));
            return tris;
        }

        /// <summary>Box whose top is a shallow ramp: a sloped top surface the layers should follow.</summary>
        public static List<Triangle> Ramp(float x0, float x1, float y0, float y1, float zLow, float zHigh)
        {
            var v = new[]
            {
                new Vector3(x0, y0, 0), new Vector3(x1, y0, 0), new Vector3(x1, y1, 0), new Vector3(x0, y1, 0),
                new Vector3(x0, y0, zLow), new Vector3(x1, y0, zHigh), new Vector3(x1, y1, zHigh), new Vector3(x0, y1, zLow),
            };
            int[][] faces =
            {
                new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 }, new[] { 4, 6, 7 },
                new[] { 0, 1, 5 }, new[] { 0, 5, 4 }, new[] { 1, 2, 6 }, new[] { 1, 6, 5 },
                new[] { 2, 3, 7 }, new[] { 2, 7, 6 }, new[] { 3, 0, 4 }, new[] { 3, 4, 7 },
            };
            return faces.Select(f => new Triangle(v[f[0]], v[f[1]], v[f[2]])).ToList();
        }

        public static double SignedVolume(IEnumerable<Triangle> tris)
        {
            return tris.Sum(t => (double)Vector3.Dot(t.a, Vector3.Cross(t.b, t.c)) / 6.0);
        }
    }
}
