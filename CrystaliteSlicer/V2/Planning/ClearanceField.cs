namespace CrystaliteSlicer.V2.Planning
{
    /// <summary>
    /// Printed height per column and the nozzle clearance it implies:
    /// Clearance(p) = max_q Height(q) - slope*|p-q|, kept up to date incrementally.
    /// </summary>
    public sealed class ClearanceField
    {
        private readonly int nx, ny;
        private readonly float slopePerCell;
        private readonly int[] source;

        /// <summary>Highest printed Z per column (-inf where nothing is printed).</summary>
        public float[] Height { get; }
        /// <summary>Lowest Z the nozzle tip may be at, per column, without hitting printed material.</summary>
        public float[] Clearance { get; }

        public ClearanceField(int nx, int ny, float slopePerCell)
        {
            this.nx = nx;
            this.ny = ny;
            this.slopePerCell = slopePerCell;
            Height = new float[nx * ny];
            Clearance = new float[nx * ny];
            source = new int[nx * ny];
            Array.Fill(Height, float.NegativeInfinity);
            Array.Fill(Clearance, float.NegativeInfinity);
            Array.Fill(source, -1);
        }

        /// <summary>Records a layer: raises each column to its top, then updates the clearance around them.</summary>
        public void Add(LayerPlan layer)
        {
            for (int a = 0; a < layer.Columns.Length; a++)
            {
                int col = layer.Columns[a];
                Height[col] = MathF.Max(Height[col], layer.Top[a]);
            }
            Update(layer.Columns);
        }

        /// <summary>
        /// Re-propagates only from columns whose height rose; propagation stops where the cone falls below the
        /// old value.
        /// </summary>
        public void Update(IReadOnlyList<int> changedColumns)
        {
            var queue = new Queue<int>();
            foreach (int p in changedColumns)
            {
                if (Height[p] > Clearance[p])
                {
                    Clearance[p] = Height[p];
                    source[p] = p;
                    queue.Enqueue(p);
                }
            }

            while (queue.Count > 0)
            {
                int p = queue.Dequeue();
                int s = source[p];
                int si = s % nx, sj = s / nx;
                int pi = p % nx, pj = p / nx;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int i = pi + dx, j = pj + dy;
                        if ((dx == 0 && dy == 0) || i < 0 || j < 0 || i >= nx || j >= ny) continue;
                        float ddx = i - si, ddy = j - sj;
                        float candidate = Height[s] - slopePerCell * MathF.Sqrt(ddx * ddx + ddy * ddy);
                        int q = i + j * nx;
                        if (candidate > Clearance[q] + 1e-6f)
                        {
                            Clearance[q] = candidate;
                            source[q] = s;
                            queue.Enqueue(q);
                        }
                    }
                }
            }
        }
    }
}
