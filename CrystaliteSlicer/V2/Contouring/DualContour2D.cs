using Clipper2Lib;
using System.Numerics;

namespace CrystaliteSlicer.V2.Contouring
{
    /// <summary>
    /// 2D dual contouring on a node grid. Each sign-changing grid edge carries an exact crossing point and,
    /// where known, the surface normal there (Hermite data). Each cell gets one vertex: the least-squares
    /// intersection of its crossings' tangent lines, which reproduces sharp corners exactly. Polygons come
    /// out counter-clockwise around inside regions.
    /// </summary>
    public static class DualContour2D
    {
        public delegate bool InsideFn(int i, int j);

        /// <summary>Crossing on the edge from inside node a to outside node b. Return false if no normal is known.</summary>
        public delegate bool CrossingFn(int ia, int ja, int ib, int jb, out Vector2 position, out Vector2 normal);

        private struct CellData
        {
            public double Ax, Ay, Bxx, Bxy, Byy, Bx, By; // mass point sum, AᵀA and Aᵀb of the QEF
            public int Count, NormalCount;
        }

        public static PathsD Extract(int nx, int ny, InsideFn inside, CrossingFn crossing, Func<int, int, Vector2> nodePosition, float cellSize)
        {
            int px = nx + 2;
            var inFlag = new bool[px * (ny + 2)];
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    inFlag[(i + 1) + (j + 1) * px] = inside(i, j);
                }
            }
            bool In(int i, int j) => inFlag[(i + 1) + (j + 1) * px];
            bool InDomain(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny;

            // Cells (i,j) for i in [-1, nx-1], j in [-1, ny-1]; corners (i..i+1, j..j+1).
            int cw = nx + 1;
            int CellIndex(int i, int j) => (i + 1) + (j + 1) * cw;
            var cells = new CellData[cw * (ny + 1)];

            void AddCrossing(int ia, int ja, int ib, int jb, int c0, int c1)
            {
                Vector2 pos, n;
                bool hasNormal;
                if (InDomain(ia, ja) && InDomain(ib, jb))
                {
                    hasNormal = crossing(ia, ja, ib, jb, out pos, out n);
                }
                else
                {
                    // Edge leaving the domain: a fake boundary halfway, callers keep it outside the area they use.
                    var pa = InDomain(ia, ja) ? nodePosition(ia, ja) : nodePosition(ib, jb) + (new Vector2(ia - ib, ja - jb) * cellSize);
                    var pb = InDomain(ib, jb) ? nodePosition(ib, jb) : nodePosition(ia, ja) + (new Vector2(ib - ia, jb - ja) * cellSize);
                    pos = (pa + pb) * 0.5f;
                    n = Vector2.Zero;
                    hasNormal = false;
                }
                foreach (int c in new[] { c0, c1 })
                {
                    ref var cell = ref cells[c];
                    cell.Ax += pos.X;
                    cell.Ay += pos.Y;
                    cell.Count++;
                    if (hasNormal)
                    {
                        double d = n.X * pos.X + n.Y * pos.Y;
                        cell.Bxx += n.X * n.X;
                        cell.Bxy += n.X * n.Y;
                        cell.Byy += n.Y * n.Y;
                        cell.Bx += n.X * d;
                        cell.By += n.Y * d;
                        cell.NormalCount++;
                    }
                }
            }

            var outA = new int[cells.Length];
            var outB = new int[cells.Length];
            Array.Fill(outA, -1);
            Array.Fill(outB, -1);
            void Link(int from, int to)
            {
                if (outA[from] < 0) outA[from] = to; else outB[from] = to;
            }

            // Horizontal edges (i,j)-(i+1,j): cells below (i,j-1) and above (i,j).
            for (int j = 0; j < ny; j++)
            {
                for (int i = -1; i < nx; i++)
                {
                    bool a = In(i, j), b = In(i + 1, j);
                    if (a == b) continue;
                    int below = CellIndex(i, j - 1), above = CellIndex(i, j);
                    if (a) { AddCrossing(i, j, i + 1, j, below, above); Link(below, above); }
                    else { AddCrossing(i + 1, j, i, j, below, above); Link(above, below); }
                }
            }
            // Vertical edges (i,j)-(i,j+1): cells left (i-1,j) and right (i,j).
            for (int j = -1; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    bool a = In(i, j), b = In(i, j + 1);
                    if (a == b) continue;
                    int left = CellIndex(i - 1, j), right = CellIndex(i, j);
                    if (a) { AddCrossing(i, j, i, j + 1, left, right); Link(right, left); }
                    else { AddCrossing(i, j + 1, i, j, left, right); Link(left, right); }
                }
            }

            var vertices = new PointD[cells.Length];
            for (int j = -1; j < ny; j++)
            {
                for (int i = -1; i < nx; i++)
                {
                    int c = CellIndex(i, j);
                    if (cells[c].Count == 0) continue;
                    vertices[c] = SolveVertex(cells[c], i, j, nodePosition, cellSize, nx, ny);
                }
            }

            int PopOut(int c)
            {
                int next = outA[c];
                if (next >= 0) { outA[c] = -1; return next; }
                next = outB[c];
                outB[c] = -1;
                return next;
            }

            // Every cell has as many outgoing as incoming links, so a walk from any cell returns to it.
            var paths = new PathsD();
            for (int start = 0; start < cells.Length; start++)
            {
                while (outA[start] >= 0 || outB[start] >= 0)
                {
                    var path = new PathD();
                    int cur = start;
                    do
                    {
                        path.Add(vertices[cur]);
                        cur = PopOut(cur);
                    } while (cur >= 0 && cur != start);
                    if (path.Count >= 3)
                    {
                        paths.Add(path);
                    }
                }
            }
            return paths;
        }

        private static PointD SolveVertex(CellData cell, int i, int j, Func<int, int, Vector2> nodePosition, float cellSize, int nx, int ny)
        {
            double mx = cell.Ax / cell.Count, my = cell.Ay / cell.Count;
            if (cell.NormalCount == 0)
            {
                return new PointD(mx, my);
            }
            // Minimise Σ(n·x - n·p)² + λ|x - m|²; λ only matters along directions the normals leave free.
            const double lambda = 1e-3;
            double a = cell.Bxx + lambda, b = cell.Bxy, d = cell.Byy + lambda;
            double rx = cell.Bx + lambda * mx, ry = cell.By + lambda * my;
            double det = a * d - b * b;
            double x = (d * rx - b * ry) / det, y = (a * ry - b * rx) / det;

            // Keep the vertex in (a slightly grown copy of) its cell; otherwise fall back to the mass point.
            int ci = Math.Clamp(i, 0, nx - 1), cj = Math.Clamp(j, 0, ny - 1);
            var origin = nodePosition(ci, cj) + new Vector2(i - ci, j - cj) * cellSize;
            double slack = 0.25 * cellSize;
            if (x < origin.X - slack || x > origin.X + cellSize + slack || y < origin.Y - slack || y > origin.Y + cellSize + slack)
            {
                return new PointD(mx, my);
            }
            return new PointD(x, y);
        }
    }
}
