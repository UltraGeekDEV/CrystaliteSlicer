using Clipper2Lib;
using CrystaliteSlicer.V2.Contouring;
using CrystaliteSlicer.V2.Dexels;
using CrystaliteSlicer.V2.Geometry;
using CrystaliteSlicer.V2.Planning;
using System.Numerics;

namespace CrystaliteSlicer.V2.Toolpath
{
    public enum PathRole { InnerWall, OuterWall, Skin, Infill }

    public sealed class LayerPath
    {
        public PathRole Role { get; init; }
        public int WallIndex { get; init; }
        public List<Vector2> Points { get; init; } = new();
        public bool Closed { get; init; }
    }

    public sealed class ToolpathSettings
    {
        public float LineWidth { get; set; } = 0.4f;
        public int WallCount { get; set; } = 3;
        public float TopBottomThickness { get; set; } = 1.0f;
        /// <summary>0..1</summary>
        public float InfillDensity { get; set; } = 0.2f;
        /// <summary>Fraction of a line width infill/skin overlaps the innermost wall.</summary>
        public float InfillOverlap { get; set; } = 0.15f;
    }

    /// <summary>
    /// The layer's heightfields (mid, top, bottom) over a padded domain around it, extended outward from
    /// the layer's own columns so outlines and path points just outside them still get sensible heights.
    /// </summary>
    public sealed class LayerSurface
    {
        public int X0 { get; init; }
        public int Y0 { get; init; }
        public int W { get; init; }
        public int H { get; init; }
        public float Cell { get; init; }
        public Vector2 Origin { get; init; }
        public float[] Mid { get; init; } = Array.Empty<float>();
        public float[] Top { get; init; } = Array.Empty<float>();
        public float[] Bottom { get; init; } = Array.Empty<float>();
        public bool[] Active { get; init; } = Array.Empty<bool>();
        public int[] Interval { get; init; } = Array.Empty<int>();

        public float SampleTop(Vector2 p) => Sample(Top, p);
        public float SampleThickness(Vector2 p) => MathF.Max(0, Sample(Top, p) - Sample(Bottom, p));

        public float Sample(float[] field, Vector2 p)
        {
            float u = Math.Clamp((p.X - Origin.X) / Cell, 0, W - 1.001f);
            float v = Math.Clamp((p.Y - Origin.Y) / Cell, 0, H - 1.001f);
            int i = (int)u, j = (int)v;
            float fu = u - i, fv = v - j;
            float a = field[i + j * W], b = field[i + 1 + j * W], c = field[i + (j + 1) * W], d = field[i + 1 + (j + 1) * W];
            return (a * (1 - fu) + b * fu) * (1 - fv) + (c * (1 - fu) + d * fu) * fv;
        }
    }

    public sealed class LayerToolpathBuilder
    {
        private const int Precision = 4;

        private readonly DexelGrid grid;
        private readonly TriangleBvh bvh;
        private readonly ToolpathSettings settings;

        public LayerToolpathBuilder(DexelGrid grid, TriangleBvh bvh, ToolpathSettings settings)
        {
            this.grid = grid;
            this.bvh = bvh;
            this.settings = settings;
        }

        public LayerSurface BuildSurface(LayerPlan plan)
        {
            float c = grid.CellSize;
            int margin = (int)MathF.Ceiling(((settings.WallCount + 2) * settings.LineWidth) / c) + 3;
            int x0 = Math.Max(0, plan.MinI - margin), x1 = Math.Min(grid.NX - 1, plan.MaxI + margin);
            int y0 = Math.Max(0, plan.MinJ - margin), y1 = Math.Min(grid.NY - 1, plan.MaxJ + margin);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;

            var mid = new float[w * h];
            var top = new float[w * h];
            var bottom = new float[w * h];
            var active = new bool[w * h];
            var interval = new int[w * h];
            var filled = new bool[w * h];
            Array.Fill(interval, -1);

            var frontier = new List<int>();
            for (int a = 0; a < plan.Columns.Length; a++)
            {
                int col = plan.Columns[a];
                int li = col % grid.NX - x0, lj = col / grid.NX - y0;
                int p = li + lj * w;
                top[p] = plan.Top[a];
                bottom[p] = plan.Bottom[a];
                mid[p] = 0.5f * (plan.Top[a] + plan.Bottom[a]);
                active[p] = true;
                filled[p] = true;
                interval[p] = plan.Interval[a];
                frontier.Add(p);
            }

            // Extend the fields ring by ring: each new node takes the average of its already-filled neighbours.
            while (frontier.Count > 0)
            {
                var ring = new List<int>();
                var seen = new HashSet<int>();
                foreach (int p in frontier)
                {
                    int pi = p % w, pj = p / w;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int i = pi + dx, j = pj + dy;
                            if (i < 0 || j < 0 || i >= w || j >= h) continue;
                            int q = i + j * w;
                            if (!filled[q] && seen.Add(q)) ring.Add(q);
                        }
                    }
                }
                foreach (int q in ring)
                {
                    int qi = q % w, qj = q / w;
                    float sm = 0, st = 0, sb = 0;
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int i = qi + dx, j = qj + dy;
                            if (i < 0 || j < 0 || i >= w || j >= h) continue;
                            int r = i + j * w;
                            if (!filled[r]) continue;
                            sm += mid[r]; st += top[r]; sb += bottom[r];
                            n++;
                        }
                    }
                    mid[q] = sm / n; top[q] = st / n; bottom[q] = sb / n;
                }
                foreach (int q in ring) filled[q] = true;
                frontier = ring;
            }

            return new LayerSurface
            {
                X0 = x0, Y0 = y0, W = w, H = h,
                Cell = c,
                Origin = grid.NodePosition(x0, y0),
                Mid = mid, Top = top, Bottom = bottom, Active = active, Interval = interval,
            };
        }

        /// <summary>The model's exact cross-section on the layer's mid-surface (inside the surface's domain).</summary>
        public PathsD ModelOutline(LayerSurface s)
        {
            bool Inside(int i, int j)
            {
                int p = i + j * s.W;
                return s.Active[p] || grid.FindInterval((s.X0 + i) + (s.Y0 + j) * grid.NX, s.Mid[p]) >= 0;
            }
            bool Crossing(int ia, int ja, int ib, int jb, out Vector2 pos, out Vector2 normal)
            {
                var pa = grid.NodePosition(s.X0 + ia, s.Y0 + ja);
                var pb = grid.NodePosition(s.X0 + ib, s.Y0 + jb);
                var a3 = new Vector3(pa, s.Mid[ia + ja * s.W]);
                var b3 = new Vector3(pb, s.Mid[ib + jb * s.W]);
                if (bvh.IntersectSegment(a3, b3, out float t, out var n3))
                {
                    pos = Vector2.Lerp(pa, pb, t);
                    normal = new Vector2(n3.X, n3.Y);
                    float len = normal.Length();
                    if (len > 0.2f)
                    {
                        normal /= len;
                        return true;
                    }
                    return false;
                }
                pos = (pa + pb) * 0.5f;
                normal = Vector2.Zero;
                return false;
            }
            return Union(DualContour2D.Extract(s.W, s.H, Inside, Crossing, (i, j) => grid.NodePosition(s.X0 + i, s.Y0 + j), grid.CellSize));
        }

        /// <summary>
        /// Where this layer may put material: its own columns plus all air (so the model outline, not this
        /// mask, decides the outer edge). Cuts halfway towards columns that belong to other layers.
        /// </summary>
        public PathsD LayerMask(LayerSurface s)
        {
            var inModel = new bool[s.W * s.H];
            for (int j = 0; j < s.H; j++)
            {
                for (int i = 0; i < s.W; i++)
                {
                    int p = i + j * s.W;
                    inModel[p] = s.Active[p] || grid.FindInterval((s.X0 + i) + (s.Y0 + j) * grid.NX, s.Mid[p]) >= 0;
                }
            }
            bool Inside(int i, int j) => s.Active[i + j * s.W] || !inModel[i + j * s.W];
            bool Crossing(int ia, int ja, int ib, int jb, out Vector2 pos, out Vector2 normal)
            {
                pos = (grid.NodePosition(s.X0 + ia, s.Y0 + ja) + grid.NodePosition(s.X0 + ib, s.Y0 + jb)) * 0.5f;
                normal = Vector2.Zero;
                return false;
            }
            return Union(DualContour2D.Extract(s.W, s.H, Inside, Crossing, (i, j) => grid.NodePosition(s.X0 + i, s.Y0 + j), grid.CellSize));
        }

        /// <summary>Where the layer is within TopBottomThickness of a top or bottom surface (plus don't-care outside the layer).</summary>
        public PathsD SkinMask(LayerSurface s)
        {
            float tt = settings.TopBottomThickness;
            float Margin(int p)
            {
                int k = s.Interval[p];
                return MathF.Min(grid.ZOut[k] - s.Top[p], s.Bottom[p] - grid.ZIn[k]) - tt;
            }
            bool Inside(int i, int j)
            {
                int p = i + j * s.W;
                return !s.Active[p] || Margin(p) < 0;
            }
            bool Crossing(int ia, int ja, int ib, int jb, out Vector2 pos, out Vector2 normal)
            {
                var pa = grid.NodePosition(s.X0 + ia, s.Y0 + ja);
                var pb = grid.NodePosition(s.X0 + ib, s.Y0 + jb);
                int a = ia + ja * s.W, b = ib + jb * s.W;
                float t = 0.5f;
                if (s.Active[a] && s.Active[b])
                {
                    float fa = Margin(a), fb = Margin(b);
                    if (fb - fa > 1e-6f) t = Math.Clamp(-fa / (fb - fa), 0, 1);
                }
                pos = Vector2.Lerp(pa, pb, t);
                normal = Vector2.Zero;
                return false;
            }
            return Union(DualContour2D.Extract(s.W, s.H, Inside, Crossing, (i, j) => grid.NodePosition(s.X0 + i, s.Y0 + j), grid.CellSize));
        }

        public List<LayerPath> BuildPaths(LayerPlan plan, LayerSurface s)
        {
            float w = settings.LineWidth;
            var outline = ModelOutline(s);
            outline = RemoveSpecks(outline, 0.25 * w * w);
            var mask = LayerMask(s);
            var result = new List<LayerPath>();

            for (int wall = 0; wall < settings.WallCount; wall++)
            {
                var ring = Clipper.InflatePaths(outline, -(wall + 0.5) * w, JoinType.Miter, EndType.Polygon, 3.0, Precision);
                foreach (var path in ClipOpen(CloseAsOpen(ring), mask))
                {
                    AddPath(result, path, wall == 0 ? PathRole.OuterWall : PathRole.InnerWall, wall);
                }
            }

            var inner = Clipper.InflatePaths(outline, -(settings.WallCount + 0.5 - settings.InfillOverlap) * w, JoinType.Miter, EndType.Polygon, 3.0, Precision);
            inner = Boolean(ClipType.Intersection, inner, mask);
            if (inner.Count == 0)
            {
                return result;
            }

            var skinMask = SkinMask(s);
            var skin = Boolean(ClipType.Intersection, inner, skinMask);
            var sparse = Boolean(ClipType.Difference, inner, skinMask);

            var bounds = Clipper.GetBounds(inner);
            float skinAngle = plan.Index % 2 == 0 ? 45 : 135;
            foreach (var path in ClipOpen(Hatch(bounds, skinAngle, w), skin))
            {
                AddPath(result, path, PathRole.Skin, 0);
            }
            if (settings.InfillDensity > 1e-3f)
            {
                double spacing = 2 * w / Math.Min(1, settings.InfillDensity);
                var lines = Hatch(bounds, 45, spacing);
                lines.AddRange(Hatch(bounds, 135, spacing));
                foreach (var path in ClipOpen(lines, sparse))
                {
                    AddPath(result, path, PathRole.Infill, 0);
                }
            }
            return result;
        }

        private static void AddPath(List<LayerPath> result, PathD path, PathRole role, int wallIndex)
        {
            if (path.Count < 2) return;
            var pts = path.Select(p => new Vector2((float)p.x, (float)p.y)).ToList();
            bool closed = pts.Count > 3 && Vector2.Distance(pts[0], pts[^1]) < 1e-3f;
            if (closed) pts.RemoveAt(pts.Count - 1);
            double length = 0;
            for (int i = 1; i < pts.Count; i++) length += Vector2.Distance(pts[i - 1], pts[i]);
            if (length < 0.05) return;
            result.Add(new LayerPath { Role = role, WallIndex = wallIndex, Points = pts, Closed = closed });
        }

        /// <summary>Parallel lines at a fixed global phase (so infill lines up between layers) covering bounds.</summary>
        private static PathsD Hatch(RectD bounds, double angleDegrees, double spacing)
        {
            double a = angleDegrees * Math.PI / 180;
            var dir = (x: Math.Cos(a), y: Math.Sin(a));
            var nrm = (x: -Math.Sin(a), y: Math.Cos(a));
            var corners = new[] { (bounds.left, bounds.top), (bounds.right, bounds.top), (bounds.left, bounds.bottom), (bounds.right, bounds.bottom) };
            double nMin = corners.Min(c => c.Item1 * nrm.x + c.Item2 * nrm.y), nMax = corners.Max(c => c.Item1 * nrm.x + c.Item2 * nrm.y);
            double dMin = corners.Min(c => c.Item1 * dir.x + c.Item2 * dir.y) - 1, dMax = corners.Max(c => c.Item1 * dir.x + c.Item2 * dir.y) + 1;
            var lines = new PathsD();
            for (long k = (long)Math.Ceiling(nMin / spacing); k * spacing <= nMax; k++)
            {
                double o = k * spacing;
                lines.Add(new PathD
                {
                    new PointD(nrm.x * o + dir.x * dMin, nrm.y * o + dir.y * dMin),
                    new PointD(nrm.x * o + dir.x * dMax, nrm.y * o + dir.y * dMax),
                });
            }
            return lines;
        }

        private static PathsD CloseAsOpen(PathsD closed)
        {
            var ret = new PathsD();
            foreach (var p in closed)
            {
                if (p.Count < 2) continue;
                var q = new PathD(p) { p[0] };
                ret.Add(q);
            }
            return ret;
        }

        private static PathsD ClipOpen(PathsD open, PathsD clip)
        {
            var clipper = new ClipperD(Precision);
            clipper.AddOpenSubject(open);
            clipper.AddClip(clip);
            var closedOut = new PathsD();
            var openOut = new PathsD();
            clipper.Execute(ClipType.Intersection, FillRule.NonZero, closedOut, openOut);
            return openOut;
        }

        private static PathsD Boolean(ClipType type, PathsD subject, PathsD clip)
        {
            var clipper = new ClipperD(Precision);
            clipper.AddSubject(subject);
            clipper.AddClip(clip);
            var result = new PathsD();
            clipper.Execute(type, FillRule.NonZero, result);
            return result;
        }

        private static PathsD Union(PathsD paths)
        {
            var clipper = new ClipperD(Precision);
            clipper.AddSubject(paths);
            var result = new PathsD();
            clipper.Execute(ClipType.Union, FillRule.NonZero, result);
            return result;
        }

        private static PathsD RemoveSpecks(PathsD paths, double minArea)
        {
            return new PathsD(paths.Where(p => Math.Abs(Clipper.Area(p)) >= minArea));
        }
    }
}
