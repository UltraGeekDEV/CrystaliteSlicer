using Models;
using System.Numerics;

namespace CrystaliteSlicer.V2.Geometry
{
    /// <summary>Bounding volume hierarchy over the mesh, used for exact segment-surface intersections.</summary>
    public sealed class TriangleBvh
    {
        private struct Node
        {
            public Vector3 Min, Max;
            public int Left, Right; // children, or -1 for a leaf
            public int Start, Count; // leaf range in order[]
        }

        private readonly Vector3[] va, vb, vc;
        private readonly int[] order;
        private readonly List<Node> nodes = new();

        public TriangleBvh(IReadOnlyList<Triangle> triangles)
        {
            int n = triangles.Count;
            va = new Vector3[n];
            vb = new Vector3[n];
            vc = new Vector3[n];
            order = new int[n];
            var centroids = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                va[i] = triangles[i].a;
                vb[i] = triangles[i].b;
                vc[i] = triangles[i].c;
                centroids[i] = (va[i] + vb[i] + vc[i]) / 3;
                order[i] = i;
            }
            if (n > 0)
            {
                Build(0, n, centroids);
            }
        }

        private int Build(int start, int count, Vector3[] centroids)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            var cmin = new Vector3(float.MaxValue);
            var cmax = new Vector3(float.MinValue);
            for (int i = start; i < start + count; i++)
            {
                int t = order[i];
                min = Vector3.Min(min, Vector3.Min(va[t], Vector3.Min(vb[t], vc[t])));
                max = Vector3.Max(max, Vector3.Max(va[t], Vector3.Max(vb[t], vc[t])));
                cmin = Vector3.Min(cmin, centroids[t]);
                cmax = Vector3.Max(cmax, centroids[t]);
            }

            int index = nodes.Count;
            nodes.Add(new Node { Min = min, Max = max, Left = -1, Right = -1, Start = start, Count = count });
            if (count <= 4)
            {
                return index;
            }

            var extent = cmax - cmin;
            int axis = extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;
            Array.Sort(order, start, count, Comparer<int>.Create((p, q) => Axis(centroids[p], axis).CompareTo(Axis(centroids[q], axis))));
            int half = count / 2;
            int left = Build(start, half, centroids);
            int right = Build(start + half, count - half, centroids);
            var node = nodes[index];
            node.Left = left;
            node.Right = right;
            nodes[index] = node;
            return index;
        }

        private static float Axis(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        /// <summary>First intersection of segment p0→p1 with the mesh; t in [0,1] along the segment.</summary>
        public bool IntersectSegment(Vector3 p0, Vector3 p1, out float t, out Vector3 normal)
        {
            t = float.MaxValue;
            normal = Vector3.Zero;
            if (nodes.Count == 0)
            {
                return false;
            }

            var dir = p1 - p0;
            var inv = new Vector3(1 / dir.X, 1 / dir.Y, 1 / dir.Z);
            // Called from several layers in parallel: traversal state stays on the stack. Median splits keep the
            // tree depth near log2(triangles), far below the 256 entries this allows.
            Span<int> stack = stackalloc int[256];
            int top = 0;
            stack[top++] = 0;
            bool hit = false;
            while (top > 0)
            {
                var node = nodes[stack[--top]];
                if (!SegmentHitsBox(p0, inv, node.Min, node.Max, MathF.Min(t, 1)))
                {
                    continue;
                }
                if (node.Left < 0)
                {
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        int tri = order[i];
                        if (RayTriangle(p0, dir, va[tri], vb[tri], vc[tri], out float tt) && tt >= 0 && tt <= 1 && tt < t)
                        {
                            t = tt;
                            normal = Vector3.Normalize(Vector3.Cross(vb[tri] - va[tri], vc[tri] - va[tri]));
                            hit = true;
                        }
                    }
                }
                else
                {
                    stack[top++] = node.Left;
                    stack[top++] = node.Right;
                }
            }
            return hit;
        }

        private static bool SegmentHitsBox(Vector3 origin, Vector3 inv, Vector3 min, Vector3 max, float tMax)
        {
            const float pad = 1e-4f;
            float t0 = 0, t1 = tMax;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = Axis(origin, axis), iv = Axis(inv, axis);
                float lo = Axis(min, axis) - pad, hi = Axis(max, axis) + pad;
                if (float.IsInfinity(iv))
                {
                    if (o < lo || o > hi) return false;
                    continue;
                }
                float a = (lo - o) * iv, b = (hi - o) * iv;
                if (a > b) (a, b) = (b, a);
                t0 = MathF.Max(t0, a);
                t1 = MathF.Min(t1, b);
                if (t0 > t1) return false;
            }
            return true;
        }

        // Double-sided Möller–Trumbore in double precision.
        private static bool RayTriangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0;
            double e1x = b.X - a.X, e1y = b.Y - a.Y, e1z = b.Z - a.Z;
            double e2x = c.X - a.X, e2y = c.Y - a.Y, e2z = c.Z - a.Z;
            double px = d.Y * e2z - d.Z * e2y, py = d.Z * e2x - d.X * e2z, pz = d.X * e2y - d.Y * e2x;
            double det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-14) return false;
            double invDet = 1 / det;
            double sx = o.X - a.X, sy = o.Y - a.Y, sz = o.Z - a.Z;
            double u = (sx * px + sy * py + sz * pz) * invDet;
            if (u < -1e-9 || u > 1 + 1e-9) return false;
            double qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            double v = (d.X * qx + d.Y * qy + d.Z * qz) * invDet;
            if (v < -1e-9 || u + v > 1 + 1e-9) return false;
            t = (float)((e2x * qx + e2y * qy + e2z * qz) * invDet);
            return true;
        }
    }
}
