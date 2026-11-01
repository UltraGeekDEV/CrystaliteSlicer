using Clipper2Lib;
using System.Numerics;

namespace CrystaliteSlicer.V2.Toolpath
{
    /// <summary>
    /// Centrelines of regions too thin for a full-width bead (features narrower than a line width, gaps between
    /// walls), with the local width at every point, so they can be printed as one variable-width line.
    /// The centreline comes from a fine raster of the region (Zhang-Suen thinning → chain tracing), then each point
    /// is re-centred between the region's exact boundaries along the local normal, which also gives its width.
    /// </summary>
    public static class ThinFeatures
    {
        public static List<(List<Vector2> points, List<float> widths)> Centerlines(PathsD region, double cell, double minLength)
        {
            var result = new List<(List<Vector2>, List<float>)>();
            if (region.Count == 0) return result;

            var raster = new RegionRaster(region, cell, pad: 1);
            int nx = raster.NX, ny = raster.NY;
            var skeleton = (bool[])raster.Inside.Clone();
            Thin(skeleton, nx, ny);

            Vector2 Pos(int p) => new((float)(raster.X0 + (p % nx + 0.5) * cell), (float)(raster.Y0 + (p / nx + 0.5) * cell));

            foreach (var chain in Trace(skeleton, nx, ny))
            {
                var path = new PathD(chain.Select(p => { var v = Pos(p); return new PointD(v.X, v.Y); }));
                double length = 0;
                for (int k = 1; k < path.Count; k++) length += Math.Sqrt(Sq(path[k], path[k - 1]));
                if (length < minLength) continue;

                // Pixel staircases would otherwise survive as vertices.
                var simple = Clipper.SimplifyPath(path, 0.75 * cell, false);
                var points = new List<Vector2>(simple.Count);
                var widths = new List<float>(simple.Count);
                for (int k = 0; k < simple.Count; k++)
                {
                    var q = simple[k];
                    var prev = simple[Math.Max(0, k - 1)];
                    var next = simple[Math.Min(simple.Count - 1, k + 1)];
                    double tx = next.x - prev.x, ty = next.y - prev.y, tl = Math.Sqrt(tx * tx + ty * ty);
                    if (tl > 0 && Across(q, -ty / tl, tx / tl, region, out double plus, out double minus))
                    {
                        double shift = 0.5 * (plus - minus);
                        q = new PointD(q.x - ty / tl * shift, q.y + tx / tl * shift);
                        points.Add(new Vector2((float)q.x, (float)q.y));
                        widths.Add((float)(plus + minus));
                    }
                    else
                    {
                        points.Add(new Vector2((float)q.x, (float)q.y));
                        widths.Add((float)(2 * DistanceToBoundary(q, region)));
                    }
                }
                if (points.Count >= 2) result.Add((points, widths));
            }
            return result;
        }

        private static double Sq(PointD a, PointD b) => (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y);

        /// <summary>Distances from q to the region boundary along +n and -n (both found, or false).</summary>
        private static bool Across(PointD q, double nx, double ny, PathsD region, out double plus, out double minus)
        {
            plus = minus = double.MaxValue;
            foreach (var poly in region)
            {
                for (int k = 0; k < poly.Count; k++)
                {
                    var a = poly[k];
                    var b = poly[(k + 1) % poly.Count];
                    double ex = b.x - a.x, ey = b.y - a.y;
                    double denom = nx * ey - ny * ex;
                    if (Math.Abs(denom) < 1e-12) continue;
                    // q + s*n = a + u*e
                    double ax = a.x - q.x, ay = a.y - q.y;
                    double sParam = (ax * ey - ay * ex) / denom;
                    double u = (ax * ny - ay * nx) / denom;
                    if (u < 0 || u > 1) continue;
                    if (sParam >= 0) plus = Math.Min(plus, sParam);
                    else minus = Math.Min(minus, -sParam);
                }
            }
            return plus < double.MaxValue && minus < double.MaxValue;
        }

        private static double DistanceToBoundary(PointD q, PathsD region)
        {
            double best = double.MaxValue;
            foreach (var poly in region)
            {
                for (int k = 0; k < poly.Count; k++)
                {
                    var a = poly[k];
                    var b = poly[(k + 1) % poly.Count];
                    double dx = b.x - a.x, dy = b.y - a.y;
                    double len2 = dx * dx + dy * dy;
                    double t = len2 > 0 ? Math.Clamp(((q.x - a.x) * dx + (q.y - a.y) * dy) / len2, 0, 1) : 0;
                    best = Math.Min(best, Sq(q, new PointD(a.x + t * dx, a.y + t * dy)));
                }
            }
            return Math.Sqrt(best);
        }

        /// <summary>Zhang-Suen thinning to a one-pixel, 8-connected skeleton. Expects a one-pixel empty border.</summary>
        private static void Thin(bool[] img, int nx, int ny)
        {
            var remove = new List<int>();
            // Only set pixels can change; gap regions are thin, so this is far fewer than the raster's area.
            var live = new List<int>();
            for (int p = 0; p < img.Length; p++) if (img[p]) live.Add(p);
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int step = 0; step < 2; step++)
                {
                    remove.Clear();
                    foreach (int p in live)
                    {
                        if (!img[p]) continue;
                        // P2..P9: N, NE, E, SE, S, SW, W, NW
                        bool p2 = img[p - nx], p3 = img[p - nx + 1], p4 = img[p + 1], p5 = img[p + nx + 1];
                        bool p6 = img[p + nx], p7 = img[p + nx - 1], p8 = img[p - 1], p9 = img[p - nx - 1];
                        int b = (p2 ? 1 : 0) + (p3 ? 1 : 0) + (p4 ? 1 : 0) + (p5 ? 1 : 0) + (p6 ? 1 : 0) + (p7 ? 1 : 0) + (p8 ? 1 : 0) + (p9 ? 1 : 0);
                        if (b < 2 || b > 6) continue;
                        int a = (!p2 && p3 ? 1 : 0) + (!p3 && p4 ? 1 : 0) + (!p4 && p5 ? 1 : 0) + (!p5 && p6 ? 1 : 0)
                              + (!p6 && p7 ? 1 : 0) + (!p7 && p8 ? 1 : 0) + (!p8 && p9 ? 1 : 0) + (!p9 && p2 ? 1 : 0);
                        if (a != 1) continue;
                        bool ok = step == 0
                            ? !(p2 && p4 && p6) && !(p4 && p6 && p8)
                            : !(p2 && p4 && p8) && !(p2 && p6 && p8);
                        if (ok) remove.Add(p);
                    }
                    foreach (int p in remove) img[p] = false;
                    changed |= remove.Count > 0;
                    if (remove.Count > 0) live.RemoveAll(p => !img[p]);
                }
            }
        }

        private static readonly (int dx, int dy)[] Neighbours =
        {
            (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (-1, -1), (1, -1),
        };

        /// <summary>Splits a skeleton into pixel chains between end points and junctions (and closed loops).</summary>
        private static List<List<int>> Trace(bool[] img, int nx, int ny)
        {
            int Degree(int p)
            {
                int n = 0, i = p % nx, j = p / nx;
                foreach (var (dx, dy) in Neighbours)
                {
                    int x = i + dx, y = j + dy;
                    if (x >= 0 && y >= 0 && x < nx && y < ny && img[x + y * nx]) n++;
                }
                return n;
            }

            var visited = new bool[img.Length];
            var junctionSteps = new HashSet<(int, int)>();
            var chains = new List<List<int>>();
            bool Walk(int start)
            {
                var chain = new List<int> { start };
                if (Degree(start) <= 2) visited[start] = true;
                int cur = start;
                while (true)
                {
                    int next = -1, i = cur % nx, j = cur / nx;
                    // Straight neighbours first, so a diagonal never skips the pixel the chain would visit next.
                    foreach (var (dx, dy) in Neighbours)
                    {
                        int x = i + dx, y = j + dy;
                        if (x < 0 || y < 0 || x >= nx || y >= ny) continue;
                        int q = x + y * nx;
                        if (!img[q] || visited[q] || (chain.Count > 1 && q == chain[^2])) continue;
                        if (chain.Count == 1 && Degree(start) > 2 && Degree(q) > 2 && !junctionSteps.Add((start, q))) continue;
                        next = q;
                        break;
                    }
                    if (next < 0) break;
                    if (chain.Count == 1 && Degree(next) > 2) junctionSteps.Add((next, start));
                    chain.Add(next);
                    if (Degree(next) > 2) break; // junction: belongs to every branch, never marked visited
                    visited[next] = true;
                    cur = next;
                }
                if (chain.Count > 1) chains.Add(chain);
                return chain.Count > 1;
            }

            for (int p = 0; p < img.Length; p++)
            {
                // A junction starts one chain per branch.
                if (img[p] && !visited[p] && Degree(p) != 2) while (Walk(p) && Degree(p) > 2) { }
            }
            for (int p = 0; p < img.Length; p++)
            {
                if (img[p] && !visited[p]) Walk(p); // closed loops
            }
            return chains;
        }
    }
}
