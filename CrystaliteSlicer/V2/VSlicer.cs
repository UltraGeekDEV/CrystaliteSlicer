using CrystaliteSlicer.V2.Dexels;
using CrystaliteSlicer.V2.Geometry;
using CrystaliteSlicer.V2.Planning;
using CrystaliteSlicer.V2.Toolpath;
using Models;
using Models.GcodeInfo;
using System.Diagnostics;
using System.Numerics;

namespace CrystaliteSlicer.V2
{
    public sealed class SliceOptions
    {
        /// <summary>Planning grid spacing (mm). Geometry comes from the mesh, so this can be about half the line width.</summary>
        public float CellSize { get; set; } = 0.2f;
        public PlannerSettings Planner { get; set; } = new();
        public ToolpathSettings Toolpath { get; set; } = new();
        /// <summary>Extra height above the collision clearance for long travels (mm).</summary>
        public float TravelHop { get; set; } = 0.3f;
        /// <summary>Line.Flow is expressed in units of this height (matches MarlinGCodeGenerator).</summary>
        public float FlowUnit { get; set; } = 0.05f;

        public static SliceOptions FromSettings()
        {
            float w = Settings.NozzleDiameter > 0 ? Settings.NozzleDiameter : 0.4f;
            float hMax = Settings.MaxLayerHeight > 0 ? Settings.MaxLayerHeight : 0.3f;
            float cell = Settings.Resolution.X > 0 ? Math.Clamp(Settings.Resolution.X, 0.05f, w) : w / 2;
            float hMin = Math.Clamp(Settings.Resolution.Z > 0 ? Settings.Resolution.Z : 0.05f, 0.02f, 0.5f * hMax);
            return new SliceOptions
            {
                CellSize = cell,
                Planner = new PlannerSettings
                {
                    MaxLayerHeight = hMax,
                    MinLayerHeight = hMin,
                    Slope = MathF.Tan(Math.Clamp(Settings.MaxSlope, 0, 60) * MathF.PI / 180),
                    SupportDistance = MathF.Max(cell, (1 - Math.Clamp(Settings.OverhangOverlap, 0, 0.9f)) * w),
                },
                Toolpath = new ToolpathSettings
                {
                    LineWidth = w,
                    WallCount = Math.Max(0, Settings.WallCount),
                    TopBottomThickness = Settings.TopThickness,
                    InfillDensity = Math.Clamp(Settings.InfillDensity, 0, 1),
                },
                TravelHop = MathF.Max(0.2f, hMax),
                FlowUnit = Settings.Resolution.Z > 0 ? Settings.Resolution.Z : 0.05f,
            };
        }
    }

    public sealed class SliceResult
    {
        public List<Line> Path { get; init; } = new();
        public int LayerCount { get; init; }
        public bool Stalled { get; init; }
        public double ModelVolume { get; init; }
        public double UnprintedVolume { get; init; }
        public IReadOnlyList<string> Unfinished { get; init; } = Array.Empty<string>();
        public string Timings { get; init; } = "";
    }

    /// <summary>
    /// V-slicing on exact geometry: Z-dexel occupancy → continuous non-planar layer planning → per-layer exact
    /// outlines (dual contouring on mesh crossings) → walls/skin/infill → lifted, collision-checked paths.
    /// </summary>
    public sealed class VSlicer
    {
        private readonly SliceOptions options;

        public VSlicer(SliceOptions options)
        {
            this.options = options;
        }

        public SliceResult Slice(IEnumerable<Triangle> triangles, Action<LayerPlan, LayerSurface, List<LayerPath>>? onLayer = null)
        {
            var sw = Stopwatch.StartNew();
            var mesh = triangles.Select(t => new Triangle(t.a, t.b, t.c)).ToList();
            float minZ = mesh.Min(t => MathF.Min(t.a.Z, MathF.Min(t.b.Z, t.c.Z)));
            foreach (var t in mesh)
            {
                t.Offset(new Vector3(0, 0, -minZ)); // the model always sits on the bed
            }

            var grid = DexelGrid.Build(mesh, options.CellSize);
            var bvh = new TriangleBvh(mesh);
            long tGeometry = sw.ElapsedMilliseconds;

            var planner = new LayerPlanner(grid, options.Planner);
            var builder = new LayerToolpathBuilder(grid, bvh, options.Toolpath);
            var output = new List<Line>();
            Vector3? position = null;
            int layers = 0;
            long tPlan = 0, tBuild = 0, tEmit = 0;

            while (true)
            {
                long t0 = sw.ElapsedMilliseconds;
                var plan = planner.NextLayer();
                long t1 = sw.ElapsedMilliseconds;
                tPlan += t1 - t0;
                if (plan == null) break;

                var surface = builder.BuildSurface(plan);
                var paths = builder.BuildPaths(plan, surface);
                long t2 = sw.ElapsedMilliseconds;
                tBuild += t2 - t1;
                onLayer?.Invoke(plan, surface, paths);
                if (paths.Count == 0) continue;

                output.Add(new NewLayerLine());
                EmitLayer(output, paths, surface, planner, ref position);
                layers++;
                tEmit += sw.ElapsedMilliseconds - t2;
            }

            return new SliceResult
            {
                Path = output,
                LayerCount = layers,
                Stalled = planner.Stalled,
                ModelVolume = grid.Volume(),
                UnprintedVolume = planner.UnprintedVolume(),
                Unfinished = planner.DescribeUnfinished(10).ToList(),
                Timings = $"geometry {tGeometry} ms, planning {tPlan} ms, outlines+paths {tBuild} ms, ordering+travel {tEmit} ms ({grid.NX}x{grid.NY} grid, {grid.IntervalTotal} intervals)",
            };
        }

        private static readonly PathRole[] RoleOrder = { PathRole.InnerWall, PathRole.OuterWall, PathRole.Skin, PathRole.Infill };

        private void EmitLayer(List<Line> output, List<LayerPath> paths, LayerSurface surface, LayerPlanner planner, ref Vector3? position)
        {
            foreach (var role in RoleOrder)
            {
                // Inner walls from the inside out, so the outer wall is laid against something.
                var remaining = paths.Where(p => p.Role == role).OrderByDescending(p => p.WallIndex).ToList();
                if (remaining.Count == 0) continue;

                output.Add(role switch
                {
                    PathRole.OuterWall => new WallLine(),
                    PathRole.InnerWall => new InnerWallLine(),
                    PathRole.Skin => new SkinLine(),
                    _ => new InfillLine(),
                });

                while (remaining.Count > 0)
                {
                    var here = position.HasValue ? new Vector2(position.Value.X, position.Value.Y) : remaining[0].Points[0];
                    int bestPath = 0, bestPoint = 0;
                    bool reverse = false;
                    float bestDist = float.MaxValue;
                    for (int p = 0; p < remaining.Count; p++)
                    {
                        var pts = remaining[p].Points;
                        if (remaining[p].Closed)
                        {
                            for (int k = 0; k < pts.Count; k++)
                            {
                                float d = Vector2.DistanceSquared(here, pts[k]);
                                if (d < bestDist) { bestDist = d; bestPath = p; bestPoint = k; reverse = false; }
                            }
                        }
                        else
                        {
                            float ds = Vector2.DistanceSquared(here, pts[0]), de = Vector2.DistanceSquared(here, pts[^1]);
                            if (ds < bestDist) { bestDist = ds; bestPath = p; bestPoint = 0; reverse = false; }
                            if (de < bestDist) { bestDist = de; bestPath = p; bestPoint = 0; reverse = true; }
                        }
                    }

                    var chosen = remaining[bestPath];
                    remaining.RemoveAt(bestPath);
                    var ordered = new List<Vector2>();
                    if (chosen.Closed)
                    {
                        for (int k = 0; k <= chosen.Points.Count; k++)
                        {
                            ordered.Add(chosen.Points[(bestPoint + k) % chosen.Points.Count]);
                        }
                    }
                    else
                    {
                        ordered.AddRange(chosen.Points);
                        if (reverse) ordered.Reverse();
                    }

                    var lifted = Lift(ordered, surface);
                    if (lifted.Count < 2) continue;
                    var start = lifted[0].pos;
                    if (position.HasValue)
                    {
                        Travel(output, position.Value, start, planner);
                    }
                    for (int k = 1; k < lifted.Count; k++)
                    {
                        float flow = 0.5f * (lifted[k - 1].thickness + lifted[k].thickness) / options.FlowUnit;
                        output.Add(new Line(lifted[k - 1].pos, lifted[k].pos, flow, false));
                    }
                    position = lifted[^1].pos;
                }
            }
        }

        /// <summary>Resample a 2D path onto the layer's top surface, then drop points a straight 3D segment covers.</summary>
        private List<(Vector3 pos, float thickness)> Lift(List<Vector2> pts, LayerSurface surface)
        {
            var dense = new List<(Vector3 pos, float thickness)>();
            float step = surface.Cell;
            for (int k = 0; k < pts.Count; k++)
            {
                if (k > 0)
                {
                    var a = pts[k - 1];
                    var b = pts[k];
                    int pieces = (int)MathF.Ceiling(Vector2.Distance(a, b) / step);
                    for (int s = 1; s < pieces; s++)
                    {
                        var p = Vector2.Lerp(a, b, s / (float)pieces);
                        dense.Add((new Vector3(p, surface.SampleTop(p)), surface.SampleThickness(p)));
                    }
                }
                dense.Add((new Vector3(pts[k], surface.SampleTop(pts[k])), surface.SampleThickness(pts[k])));
            }

            var simplified = new List<(Vector3 pos, float thickness)> { dense[0] };
            int anchor = 0;
            for (int k = 2; k < dense.Count; k++)
            {
                if (!Covers(dense, anchor, k))
                {
                    simplified.Add(dense[k - 1]);
                    anchor = k - 1;
                }
            }
            if (dense.Count > 1) simplified.Add(dense[^1]);
            return simplified;
        }

        private static bool Covers(List<(Vector3 pos, float thickness)> pts, int from, int to)
        {
            var a = pts[from].pos;
            var b = pts[to].pos;
            var ab = b - a;
            float len2 = ab.LengthSquared();
            for (int k = from + 1; k < to; k++)
            {
                var p = pts[k].pos;
                float t = len2 > 0 ? Math.Clamp(Vector3.Dot(p - a, ab) / len2, 0, 1) : 0;
                if (Vector3.DistanceSquared(a + ab * t, p) > 0.003f * 0.003f) return false;
                float expected = pts[from].thickness + (pts[to].thickness - pts[from].thickness) * t;
                if (MathF.Abs(pts[k].thickness - expected) > 0.01f) return false;
            }
            return true;
        }

        /// <summary>Travel that never goes below the clearance of anything printed so far.</summary>
        private void Travel(List<Line> output, Vector3 from, Vector3 to, LayerPlanner planner)
        {
            var grid = planner.Grid;
            var clearance = planner.Clearance;
            var a = new Vector2(from.X, from.Y);
            var b = new Vector2(to.X, to.Y);
            float length = Vector2.Distance(a, b);
            int samples = Math.Max(1, (int)MathF.Ceiling(length / grid.CellSize));
            float needed = float.NegativeInfinity;
            for (int s = 0; s <= samples; s++)
            {
                var p = Vector2.Lerp(a, b, s / (float)samples);
                float u = (float)((p.X - grid.OriginX) / grid.CellSize), v = (float)((p.Y - grid.OriginY) / grid.CellSize);
                int i0 = Math.Clamp((int)MathF.Floor(u), 0, grid.NX - 1), j0 = Math.Clamp((int)MathF.Floor(v), 0, grid.NY - 1);
                int i1 = Math.Min(i0 + 1, grid.NX - 1), j1 = Math.Min(j0 + 1, grid.NY - 1);
                needed = MathF.Max(needed, MathF.Max(MathF.Max(clearance[i0 + j0 * grid.NX], clearance[i1 + j0 * grid.NX]),
                                                     MathF.Max(clearance[i0 + j1 * grid.NX], clearance[i1 + j1 * grid.NX])));
            }

            float ends = MathF.Max(from.Z, to.Z);
            if (length <= 2 * options.Toolpath.LineWidth && needed <= ends + 1e-3f)
            {
                output.Add(new Line(from, to, 0, true));
                return;
            }
            float z = MathF.Max(ends, needed) + options.TravelHop;
            var up = new Vector3(from.X, from.Y, z);
            var over = new Vector3(to.X, to.Y, z);
            output.Add(new Line(from, up, 0, true));
            output.Add(new Line(up, over, 0, true));
            output.Add(new Line(over, to, 0, true));
        }
    }
}
