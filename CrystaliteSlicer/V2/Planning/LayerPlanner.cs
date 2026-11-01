using CrystaliteSlicer.V2.Dexels;

namespace CrystaliteSlicer.V2.Planning
{
    public sealed class PlannerSettings
    {
        /// <summary>Thickest a single layer may be in any column (mm).</summary>
        public float MaxLayerHeight { get; set; } = 0.3f;
        /// <summary>Thinnest layer worth printing, except when finishing an exact top surface (mm).</summary>
        public float MinLayerHeight { get; set; } = 0.05f;
        /// <summary>
        /// Thinnest bead a column prefers to wait for when it is held down by material that has not started yet
        /// (e.g. behind an overhang that spreads outward one bead per layer): it sits out until it can lay this
        /// much instead of creeping up a few hundredths per layer. Falls back to MinLayerHeight when nothing could print.
        /// </summary>
        public float PreferredLayerHeight { get; set; } = 0.2f;
        /// <summary>tan of the nozzle clearance angle: how much higher material may be per mm of distance.</summary>
        public float Slope { get; set; } = MathF.Tan(10 * MathF.PI / 180);
        /// <summary>How far sideways a layer may grow from printed material (mm).</summary>
        public float SupportDistance { get; set; } = 0.4f;
        public float BedZ { get; set; } = 0;
    }

    /// <summary>One non-planar layer: for each column it covers, the bead goes from Bottom to Top.</summary>
    public sealed class LayerPlan
    {
        public int Index { get; init; }
        public int[] Columns { get; init; } = Array.Empty<int>();
        public float[] Bottom { get; init; } = Array.Empty<float>();
        public float[] Top { get; init; } = Array.Empty<float>();
        /// <summary>Global dexel interval index each column is printing.</summary>
        public int[] Interval { get; init; } = Array.Empty<int>();
        public int MinI { get; init; }
        public int MaxI { get; init; }
        public int MinJ { get; init; }
        public int MaxJ { get; init; }
    }

    /// <summary>
    /// Grows non-planar layers over a <see cref="DexelGrid"/> with continuous heights.
    /// Per layer: the columns that can print (started, on the bed, or supported sideways) want to rise by
    /// up to MaxLayerHeight; the layer top is the largest surface under that which respects the nozzle
    /// clearance cone within the layer; columns that would end below the clearance of already printed
    /// material, or would print a too-thin bead, sit the layer out.
    /// </summary>
    public sealed class LayerPlanner
    {
        private const float Eps = 1e-5f;

        private readonly DexelGrid grid;
        private readonly PlannerSettings settings;
        private readonly int nx, ny;
        private readonly float slopePerCell;
        /// <summary>
        /// Slightly steeper-than-needed cone for the in-layer surface: nearest-source propagation is off by a few
        /// tenths of a percent over long distances, which waiting columns (large in-layer height steps) expose.
        /// </summary>
        private readonly float layerSlopePerCell;
        private readonly (int dx, int dy)[] supportStencil;

        private readonly float[] printedTop;
        /// <summary>Intervals that can never print: the clearance of neighbouring material is already above them.</summary>
        private readonly bool[] blocked;
        private readonly int[] current;
        private readonly ClearanceField field;
        private readonly float[] printedHeight;
        private readonly float[] clearance;

        private readonly bool[] inProgressFlag;
        private List<int> inProgress = new();
        private readonly bool[] watchFlag;
        private List<int> watch = new();
        private readonly float[] upper;
        private readonly bool[] activeMark;
        private readonly bool[] pendingMark;

        private int layerIndex;

        public DexelGrid Grid => grid;
        public PlannerSettings Settings => settings;
        /// <summary>Highest printed Z per column (-inf where nothing is printed).</summary>
        public float[] PrintedHeight => printedHeight;
        /// <summary>Lowest Z the nozzle tip may be at, per column, without hitting printed material.</summary>
        public float[] Clearance => clearance;
        /// <summary>True when layer generation stopped with printable-looking material left over.</summary>
        public bool Stalled { get; private set; }
        /// <summary>Layers that had to ignore pending columns or drop waiting columns to make progress.</summary>
        public int FallbackLayers { get; private set; }

        public LayerPlanner(DexelGrid grid, PlannerSettings settings)
        {
            this.grid = grid;
            this.settings = settings;
            nx = grid.NX;
            ny = grid.NY;
            int n = nx * ny;
            slopePerCell = settings.Slope * grid.CellSize;
            layerSlopePerCell = 0.99f * slopePerCell;
            supportStencil = ConeTransform.DiskStencil(Math.Max(1, settings.SupportDistance / grid.CellSize));

            printedTop = new float[grid.IntervalTotal];
            Array.Fill(printedTop, float.NaN);
            blocked = new bool[grid.IntervalTotal];
            current = new int[n];
            field = new ClearanceField(nx, ny, slopePerCell);
            printedHeight = field.Height;
            clearance = field.Clearance;
            inProgressFlag = new bool[n];
            watchFlag = new bool[n];
            upper = new float[n];
            activeMark = new bool[n];
            pendingMark = new bool[n];
            Array.Fill(upper, float.PositiveInfinity);

            for (int col = 0; col < n; col++)
            {
                current[col] = grid.ColumnStart[col] < grid.ColumnStart[col + 1] ? grid.ColumnStart[col] : -1;
                if (current[col] >= 0 && OnBed(grid.ZIn[current[col]]))
                {
                    AddWatch(col);
                }
            }
        }

        private bool Started(int interval) => !float.IsNaN(printedTop[interval]);

        // Within one layer of the bed counts as on it: real meshes have chamfered or slightly tilted undersides.
        private bool OnBed(float zIn) => zIn <= settings.BedZ + settings.MaxLayerHeight;

        private void AddWatch(int col)
        {
            if (!watchFlag[col])
            {
                watchFlag[col] = true;
                watch.Add(col);
            }
        }

        private enum Support { None, Near, Supported }

        private Support Evaluate(int col)
        {
            float z = grid.ZIn[current[col]];
            if (OnBed(z))
            {
                return Support.Supported;
            }
            int ci = col % nx, cj = col / nx;
            bool near = false;
            foreach (var (dx, dy) in supportStencil)
            {
                int i = ci + dx, j = cj + dy;
                if ((dx == 0 && dy == 0) || i < 0 || j < 0 || i >= nx || j >= ny)
                {
                    continue;
                }
                int q = i + j * nx;
                for (int k = grid.ColumnStart[q]; k < grid.ColumnStart[q + 1]; k++)
                {
                    float top = printedTop[k];
                    if (float.IsNaN(top) || grid.ZIn[k] > z + settings.MaxLayerHeight + Eps)
                    {
                        continue;
                    }
                    if (top >= z + 0.5f * settings.MinLayerHeight)
                    {
                        return Support.Supported;
                    }
                    if (top >= z - settings.MaxLayerHeight)
                    {
                        near = true;
                    }
                }
            }
            return near ? Support.Near : Support.None;
        }

        /// <summary>Plans the next layer, or returns null when nothing more can be printed.</summary>
        public LayerPlan? NextLayer()
        {
            var active = inProgress.Where(c => !blocked[current[c]]).ToList();
            var pending = new List<int>();
            var keptWatch = new List<int>(watch.Count);
            foreach (int col in watch)
            {
                if (current[col] < 0 || Started(current[col]) || blocked[current[col]])
                {
                    watchFlag[col] = false;
                    continue;
                }
                switch (Evaluate(col))
                {
                    case Support.Supported:
                        active.Add(col);
                        keptWatch.Add(col);
                        break;
                    case Support.Near:
                        keptWatch.Add(col);
                        break;
                    default:
                        watchFlag[col] = false;
                        break;
                }
            }
            watch = keptWatch;

            if (active.Count == 0)
            {
                return Finish();
            }

            // Every interval still waiting to start caps the layer at its first bead's top: nothing may climb
            // so far ahead (through the clearance cone) that it blocks material not yet reachable, e.g. a rim
            // that only connects through spokes on the far side of a gear.
            foreach (int col in active) activeMark[col] = true;
            for (int col = 0; col < nx * ny; col++)
            {
                int k = current[col];
                if (k >= 0 && !activeMark[col] && !Started(k) && !blocked[k])
                {
                    pending.Add(col);
                }
            }
            foreach (int col in active) activeMark[col] = false;

            var bottom = new float[active.Count];
            var alive = new bool[active.Count];
            int minI = int.MaxValue, maxI = int.MinValue, minJ = int.MaxValue, maxJ = int.MinValue;
            void Grow(int col)
            {
                int i = col % nx, j = col / nx;
                minI = Math.Min(minI, i); maxI = Math.Max(maxI, i);
                minJ = Math.Min(minJ, j); maxJ = Math.Max(maxJ, j);
            }
            for (int a = 0; a < active.Count; a++)
            {
                int col = active[a], k = current[col];
                bottom[a] = Started(k) ? printedTop[k] : grid.ZIn[k];
                float want = bottom[a] + settings.MaxLayerHeight;
                // Never leave a sliver thinner than MinLayerHeight to finish later: stop that much short instead.
                if (want < grid.ZOut[k] && grid.ZOut[k] - want < settings.MinLayerHeight)
                {
                    want = grid.ZOut[k] - settings.MinLayerHeight;
                }
                upper[col] = MathF.Min(want, grid.ZOut[k]);
                alive[a] = true;
                Grow(col);
            }
            foreach (int col in pending)
            {
                int k = current[col];
                upper[col] = MathF.Min(upper[col], MathF.Min(grid.ZIn[k] + settings.MaxLayerHeight, grid.ZOut[k]));
                if (upper[col] < clearance[col] - 1e-4f)
                {
                    blocked[k] = true;
                    upper[col] = float.PositiveInfinity;
                    continue;
                }
                Grow(col);
            }
            // Clearance only ever rises, so a column whose highest possible top is already under it never prints.
            // Left in, it would cap its whole neighbourhood through the slope constraint.
            for (int a = 0; a < active.Count; a++)
            {
                int col = active[a];
                if (upper[col] < clearance[col] - 1e-4f)
                {
                    blocked[current[col]] = true;
                    inProgressFlag[col] = false;
                    alive[a] = false;
                    upper[col] = float.PositiveInfinity;
                }
            }
            if (!alive.Any(x => x))
            {
                return Finish();
            }

            // Solve the layer top on the sources' bounding box only: straight lines between sources stay inside it.
            const int pad = 1;
            int x0 = Math.Max(0, minI - pad), x1 = Math.Min(nx - 1, maxI + pad);
            int y0 = Math.Max(0, minJ - pad), y1 = Math.Min(ny - 1, maxJ + pad);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var localU = new float[w * h];
            var localT = new float[w * h];
            var localBest = new int[w * h];
            var candidates = (bool[])alive.Clone();

            // Columns that cannot print a good bead this layer sit it out but stay sources of the cone, so no
            // neighbour can outrun them and block them for good. If that leaves nothing printable, retry
            // without the waiting intervals' caps (a truly unprintable feature would hold everything), and as a last resort
            // drop non-printing columns as sources one round at a time. The first attempt also makes columns that a
            // waiting interval or an overhang front holds to less than PreferredLayerHeight wait for a thicker bead. (Only those: making
            // every thin column wait splits ordinary sloped layers into out-of-phase groups.)
            float preferred = MathF.Max(settings.MinLayerHeight, MathF.Min(settings.PreferredLayerHeight, settings.MaxLayerHeight));
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Array.Copy(candidates, alive, alive.Length);
                float minThickness = attempt == 0 ? preferred : settings.MinLayerHeight;
                if (attempt == 2)
                {
                    foreach (int col in pending) upper[col] = float.PositiveInfinity;
                }
                for (int round = 0; round < (attempt == 3 ? 64 : 1); round++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        Array.Copy(upper, x0 + (y0 + j) * nx, localU, j * w, w);
                    }
                    // Pure cone, both here and in the clearance: a flat-face zone is not a metric (chaining it
                    // forces every layer flat), and using it only for clearance lets a neighbour legally climb
                    // past a column that sat out one layer, blocking it for good. The nozzle face may therefore
                    // touch up to Slope * face radius (~0.035 mm at 10° and a 0.4 mm nozzle) above its tip.
                    ConeTransform.ConeMin(localU, w, h, layerSlopePerCell, localT, localBest);
                    if (attempt == 0)
                    {
                        // Waiting intervals and overhang fronts (first beads off the bed) are what hold columns down.
                        foreach (int col in pending) pendingMark[col] = true;
                        foreach (int col in active)
                        {
                            int k = current[col];
                            if (!Started(k) && !OnBed(grid.ZIn[k])) pendingMark[col] = true;
                        }
                    }

                    bool removed = false;
                    for (int a = 0; a < active.Count; a++)
                    {
                        if (!alive[a]) continue;
                        int col = active[a], k = current[col];
                        float top = localT[(col % nx - x0) + (col / nx - y0) * w];
                        bool finishing = top >= grid.ZOut[k] - Eps;
                        int source = localBest[(col % nx - x0) + (col / nx - y0) * w];
                        bool heldByPending = source >= 0 && pendingMark[x0 + source % w + (y0 + source / w) * nx];
                        float needed = heldByPending ? minThickness : settings.MinLayerHeight;
                        alive[a] = top >= clearance[col] - 1e-4f
                            && top > bottom[a] + Eps
                            && (top - bottom[a] >= needed - Eps || finishing);
                        if (!alive[a] && attempt == 3)
                        {
                            upper[col] = float.PositiveInfinity;
                            removed = true;
                        }
                    }
                    foreach (int col in pending) pendingMark[col] = false;
                    foreach (int col in active) pendingMark[col] = false;
                    if (!removed) break;
                }
                if (alive.Any(x => x)) { if (attempt > 1) FallbackLayers++; break; }
            }

            var columns = new List<int>();
            var bottoms = new List<float>();
            var tops = new List<float>();
            var intervals = new List<int>();
            minI = int.MaxValue; maxI = int.MinValue; minJ = int.MaxValue; maxJ = int.MinValue;
            var touched = new List<int>();
            for (int a = 0; a < active.Count; a++)
            {
                int col = active[a], k = current[col];
                upper[col] = float.PositiveInfinity;
                if (!alive[a]) continue;

                float top = localT[(col % nx - x0) + (col / nx - y0) * w];
                bool finished = top >= grid.ZOut[k] - Eps;
                if (finished) top = grid.ZOut[k];

                columns.Add(col);
                bottoms.Add(bottom[a]);
                tops.Add(top);
                intervals.Add(k);
                Grow(col);
                touched.Add(col);

                printedTop[k] = top;
                printedHeight[col] = MathF.Max(printedHeight[col], top);
                if (finished)
                {
                    inProgressFlag[col] = false;
                    current[col] = k + 1 < grid.ColumnStart[col + 1] ? k + 1 : -1;
                }
                else if (!inProgressFlag[col])
                {
                    inProgressFlag[col] = true;
                }
            }
            foreach (int col in pending)
            {
                upper[col] = float.PositiveInfinity;
            }

            if (columns.Count == 0)
            {
                return Finish();
            }

            var nextInProgress = new List<int>();
            var seen = new HashSet<int>();
            foreach (int col in inProgress.Concat(columns))
            {
                if (inProgressFlag[col] && seen.Add(col))
                {
                    nextInProgress.Add(col);
                }
            }
            inProgress = nextInProgress;

            foreach (int col in touched)
            {
                int ci = col % nx, cj = col / nx;
                foreach (var (dx, dy) in supportStencil)
                {
                    int i = ci + dx, j = cj + dy;
                    if (i < 0 || j < 0 || i >= nx || j >= ny) continue;
                    int q = i + j * nx;
                    if (current[q] >= 0 && !Started(current[q]))
                    {
                        AddWatch(q);
                    }
                }
            }

            field.Update(touched);

            return new LayerPlan
            {
                Index = layerIndex++,
                Columns = columns.ToArray(),
                Bottom = bottoms.ToArray(),
                Top = tops.ToArray(),
                Interval = intervals.ToArray(),
                MinI = minI,
                MaxI = maxI,
                MinJ = minJ,
                MaxJ = maxJ,
            };
        }

        private LayerPlan? Finish()
        {
            // Leftovers thinner than a bead (tessellation slivers on top surfaces) do not count as a stall.
            for (int col = 0; col < nx * ny && !Stalled; col++)
            {
                for (int k = current[col]; k >= 0 && k < grid.ColumnStart[col + 1]; k++)
                {
                    float from = float.IsNaN(printedTop[k]) ? grid.ZIn[k] : printedTop[k];
                    if (grid.ZOut[k] - from >= settings.MinLayerHeight)
                    {
                        Stalled = true;
                        break;
                    }
                }
            }
            return null;
        }

        /// <summary>Columns left unfinished, ones that started (and then got stuck) first.</summary>
        public string DescribeColumn(int col)
        {
            var parts = new List<string>();
            for (int k = grid.ColumnStart[col]; k < grid.ColumnStart[col + 1]; k++)
            {
                parts.Add($"[{grid.ZIn[k]:F2}..{grid.ZOut[k]:F2} printed {printedTop[k]:F2}{(blocked[k] ? " blocked" : "")}]");
            }
            return $"H {printedHeight[col]:F2} clearance {clearance[col]:F2} " + string.Join(" ", parts);
        }

        public IEnumerable<string> DescribeUnfinished(int max = 20)
        {
            var columns = Enumerable.Range(0, nx * ny).Where(c => current[c] >= 0)
                .OrderBy(c => Started(current[c]) ? 0 : 1).Take(max);
            foreach (int col in columns)
            {
                int k = current[col];
                var pos = grid.NodePosition(col % nx, col / nx);
                yield return $"({pos.X:F2},{pos.Y:F2}) interval {grid.ZIn[k]:F3}..{grid.ZOut[k]:F3} printed {printedTop[k]:F3} clearance {clearance[col]:F3}" + (blocked[k] ? " blocked" : $" support {Evaluate(col)}");
            }
        }

        /// <summary>Volume (mm³) of solid that was never assigned to a layer.</summary>
        public double UnprintedVolume()
        {
            double sum = 0;
            for (int k = 0; k < grid.IntervalTotal; k++)
            {
                float from = float.IsNaN(printedTop[k]) ? grid.ZIn[k] : printedTop[k];
                sum += Math.Max(0, grid.ZOut[k] - from);
            }
            return sum * grid.CellSize * grid.CellSize;
        }
    }
}
