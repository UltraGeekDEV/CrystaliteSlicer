using Models;
using System.Numerics;

namespace CrystaliteSlicer.V2.Dexels
{
    /// <summary>
    /// Z-dexel occupancy: for every node of a regular XY grid, the exact Z intervals where the
    /// vertical line through that node is inside the mesh. Replaces the 3D voxel array: memory is
    /// O(columns * crossings) and the Z values are exact floats, so there is no Z resolution.
    /// </summary>
    public sealed class DexelGrid
    {
        public float CellSize { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public int NX { get; }
        public int NY { get; }

        /// <summary>Column c owns intervals ColumnStart[c] .. ColumnStart[c+1]-1.</summary>
        public int[] ColumnStart { get; }
        public float[] ZIn { get; }
        public float[] ZOut { get; }

        public int ColumnCount => NX * NY;
        public int IntervalTotal => ZIn.Length;

        private DexelGrid(float cellSize, double originX, double originY, int nx, int ny, int[] columnStart, float[] zIn, float[] zOut)
        {
            CellSize = cellSize;
            OriginX = originX;
            OriginY = originY;
            NX = nx;
            NY = ny;
            ColumnStart = columnStart;
            ZIn = zIn;
            ZOut = zOut;
        }

        public int Index(int i, int j) => i + j * NX;
        public bool InBounds(int i, int j) => i >= 0 && j >= 0 && i < NX && j < NY;
        public Vector2 NodePosition(int i, int j) => new Vector2((float)(OriginX + i * (double)CellSize), (float)(OriginY + j * (double)CellSize));

        /// <summary>Global interval index containing z in column col, or -1.</summary>
        public int FindInterval(int col, float z)
        {
            for (int k = ColumnStart[col]; k < ColumnStart[col + 1]; k++)
            {
                if (z >= ZIn[k] && z <= ZOut[k])
                {
                    return k;
                }
            }
            return -1;
        }

        public double Volume()
        {
            double sum = 0;
            for (int k = 0; k < ZIn.Length; k++)
            {
                sum += ZOut[k] - ZIn[k];
            }
            return sum * CellSize * CellSize;
        }

        private struct Crossing
        {
            public int Col;
            public float Z;
            public sbyte Dir;
        }

        public static DexelGrid Build(IReadOnlyList<Triangle> triangles, float cellSize, int marginCells = 3)
        {
            if (triangles.Count == 0)
            {
                throw new ArgumentException("Mesh has no triangles");
            }

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var t in triangles)
            {
                minX = MathF.Min(minX, MathF.Min(t.a.X, MathF.Min(t.b.X, t.c.X)));
                minY = MathF.Min(minY, MathF.Min(t.a.Y, MathF.Min(t.b.Y, t.c.Y)));
                maxX = MathF.Max(maxX, MathF.Max(t.a.X, MathF.Max(t.b.X, t.c.X)));
                maxY = MathF.Max(maxY, MathF.Max(t.a.Y, MathF.Max(t.b.Y, t.c.Y)));
            }

            // Nodes sit half a cell plus a tiny irrational jitter off the round grid, so CAD meshes with
            // round coordinates rarely put a vertex exactly on a node. The top-left rule keeps it watertight anyway.
            double jitter = cellSize * (0.5 + 0.000618034);
            double originX = Math.Floor(minX / cellSize) * cellSize - marginCells * cellSize + jitter;
            double originY = Math.Floor(minY / cellSize) * cellSize - marginCells * cellSize + jitter;
            int nx = (int)Math.Ceiling((maxX - originX) / cellSize) + marginCells + 1;
            int ny = (int)Math.Ceiling((maxY - originY) / cellSize) + marginCells + 1;

            var buckets = new List<List<Crossing>>();
            Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, triangles.Count, Math.Max(256, triangles.Count / (Environment.ProcessorCount * 4))),
                () => new List<Crossing>(),
                (range, _, local) =>
                {
                    for (int t = range.Item1; t < range.Item2; t++)
                    {
                        Rasterize(triangles[t], originX, originY, cellSize, nx, ny, local);
                    }
                    return local;
                },
                local => { lock (buckets) { buckets.Add(local); } });

            var all = new Crossing[buckets.Sum(x => x.Count)];
            int fill = 0;
            foreach (var b in buckets)
            {
                b.CopyTo(all, fill);
                fill += b.Count;
            }
            // Entries before exits at equal Z, so touching solids merge instead of splitting.
            Array.Sort(all, (p, q) => p.Col != q.Col ? p.Col.CompareTo(q.Col) : p.Z != q.Z ? p.Z.CompareTo(q.Z) : q.Dir.CompareTo(p.Dir));

            var columnStart = new int[nx * ny + 1];
            var zIn = new List<float>();
            var zOut = new List<float>();
            const float mergeGap = 1e-5f;

            int idx = 0;
            for (int col = 0; col < nx * ny; col++)
            {
                columnStart[col] = zIn.Count;
                int firstOfColumn = zIn.Count;
                int winding = 0;
                float start = 0;
                while (idx < all.Length && all[idx].Col == col)
                {
                    var c = all[idx++];
                    int before = winding;
                    winding += c.Dir;
                    if (before <= 0 && winding > 0)
                    {
                        start = c.Z;
                    }
                    else if (before > 0 && winding <= 0)
                    {
                        if (c.Z - start <= mergeGap)
                        {
                            continue;
                        }
                        if (zIn.Count > firstOfColumn && start - zOut[^1] <= mergeGap)
                        {
                            zOut[^1] = c.Z;
                        }
                        else
                        {
                            zIn.Add(start);
                            zOut.Add(c.Z);
                        }
                    }
                }
            }
            columnStart[nx * ny] = zIn.Count;

            return new DexelGrid(cellSize, originX, originY, nx, ny, columnStart, zIn.ToArray(), zOut.ToArray());
        }

        // Edge function with a canonical endpoint order, so the two triangles sharing an edge get exactly
        // opposite values and a node on that edge is claimed by exactly one of them.
        private static double Edge(double x0, double y0, double x1, double y1, double px, double py)
        {
            bool swap = x0 > x1 || (x0 == x1 && y0 > y1);
            if (swap)
            {
                (x0, x1) = (x1, x0);
                (y0, y1) = (y1, y0);
            }
            double v = (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
            return swap ? -v : v;
        }

        // Top-left rule for a counter-clockwise triangle in a y-up frame.
        private static bool IsTopLeft(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0, dy = y1 - y0;
            return dy < 0 || (dy == 0 && dx < 0);
        }

        private static void Rasterize(Triangle tri, double originX, double originY, float cell, int nx, int ny, List<Crossing> output)
        {
            // Work in cell units so nodes are exactly the integers.
            double ax = (tri.a.X - originX) / cell, ay = (tri.a.Y - originY) / cell;
            double bx = (tri.b.X - originX) / cell, by = (tri.b.Y - originY) / cell;
            double cx = (tri.c.X - originX) / cell, cy = (tri.c.Y - originY) / cell;
            double az = tri.a.Z, bz = tri.b.Z, cz = tri.c.Z;

            double area2 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area2) < 1e-12)
            {
                return; // vertical in XY: never crosses a Z ray
            }
            // Outward normal pointing up means a ray going up leaves the solid here.
            sbyte dir = area2 > 0 ? (sbyte)-1 : (sbyte)1;
            if (area2 < 0)
            {
                (bx, cx) = (cx, bx);
                (by, cy) = (cy, by);
                (bz, cz) = (cz, bz);
            }

            int i0 = Math.Max(0, (int)Math.Ceiling(Math.Min(ax, Math.Min(bx, cx))));
            int i1 = Math.Min(nx - 1, (int)Math.Floor(Math.Max(ax, Math.Max(bx, cx))));
            int j0 = Math.Max(0, (int)Math.Ceiling(Math.Min(ay, Math.Min(by, cy))));
            int j1 = Math.Min(ny - 1, (int)Math.Floor(Math.Max(ay, Math.Max(by, cy))));

            bool tlA = IsTopLeft(bx, by, cx, cy);
            bool tlB = IsTopLeft(cx, cy, ax, ay);
            bool tlC = IsTopLeft(ax, ay, bx, by);

            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    double w0 = Edge(bx, by, cx, cy, i, j);
                    if (w0 < 0 || (w0 == 0 && !tlA)) continue;
                    double w1 = Edge(cx, cy, ax, ay, i, j);
                    if (w1 < 0 || (w1 == 0 && !tlB)) continue;
                    double w2 = Edge(ax, ay, bx, by, i, j);
                    if (w2 < 0 || (w2 == 0 && !tlC)) continue;
                    double sum = w0 + w1 + w2;
                    if (sum <= 0) continue;
                    output.Add(new Crossing
                    {
                        Col = i + j * nx,
                        Z = (float)((w0 * az + w1 * bz + w2 * cz) / sum),
                        Dir = dir,
                    });
                }
            }
        }
    }
}
