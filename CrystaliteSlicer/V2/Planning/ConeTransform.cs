namespace CrystaliteSlicer.V2.Planning
{
    /// <summary>
    /// Min/max convolutions of a grid field with a cone, i.e. result(p) = min_q src(q) + slope*|p-q|.
    /// Uses two raster passes that propagate the winning source cell (not just its value), which keeps the
    /// distance Euclidean instead of the L1/chessboard error a value-only sweep has.
    /// </summary>
    public static class ConeTransform
    {
        private static readonly (int dx, int dy)[] ForwardNeighbours = { (-1, 0), (-1, -1), (0, -1), (1, -1) };
        private static readonly (int dx, int dy)[] BackwardNeighbours = { (1, 0), (1, 1), (0, 1), (-1, 1) };

        /// <summary>result(p) = min over q with finite src of src(q) + slopePerCell * |p-q| (in cells).</summary>
        public static void ConeMin(float[] src, int nx, int ny, float slopePerCell, float[] result, int[] best)
        {
            int n = nx * ny;
            for (int p = 0; p < n; p++)
            {
                result[p] = src[p];
                best[p] = float.IsPositiveInfinity(src[p]) ? -1 : p;
            }

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    foreach (var (dx, dy) in ForwardNeighbours)
                    {
                        Relax(src, nx, ny, slopePerCell, result, best, i, j, i + dx, j + dy);
                    }
                }
                for (int i = nx - 1; i >= 0; i--)
                {
                    Relax(src, nx, ny, slopePerCell, result, best, i, j, i + 1, j);
                }
            }
            for (int j = ny - 1; j >= 0; j--)
            {
                for (int i = nx - 1; i >= 0; i--)
                {
                    foreach (var (dx, dy) in BackwardNeighbours)
                    {
                        Relax(src, nx, ny, slopePerCell, result, best, i, j, i + dx, j + dy);
                    }
                }
                for (int i = 0; i < nx; i++)
                {
                    Relax(src, nx, ny, slopePerCell, result, best, i, j, i - 1, j);
                }
            }
        }

        /// <summary>result(p) = max over q with finite src of src(q) - slopePerCell * |p-q|.</summary>
        public static void ConeMax(float[] src, int nx, int ny, float slopePerCell, float[] result, int[] best, float[] scratch)
        {
            for (int p = 0; p < src.Length; p++)
            {
                scratch[p] = -src[p];
            }
            ConeMin(scratch, nx, ny, slopePerCell, result, best);
            for (int p = 0; p < src.Length; p++)
            {
                result[p] = -result[p];
            }
        }

        private static void Relax(float[] src, int nx, int ny, float k, float[] result, int[] best, int i, int j, int ni, int nj)
        {
            if (ni < 0 || nj < 0 || ni >= nx || nj >= ny)
            {
                return;
            }
            int s = best[ni + nj * nx];
            if (s < 0)
            {
                return;
            }
            int si = s % nx, sj = s / nx;
            float dx = i - si, dy = j - sj;
            float candidate = src[s] + k * MathF.Sqrt(dx * dx + dy * dy);
            int p = i + j * nx;
            if (candidate < result[p])
            {
                result[p] = candidate;
                best[p] = s;
            }
        }

        public static (int dx, int dy)[] DiskStencil(float radiusCells)
        {
            int r = (int)MathF.Floor(radiusCells + 1e-4f);
            var ret = new List<(int, int)>();
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy <= radiusCells * radiusCells + 1e-4f)
                    {
                        ret.Add((dx, dy));
                    }
                }
            }
            return ret.ToArray();
        }

        public static void DiskMin(float[] src, int nx, int ny, (int dx, int dy)[] stencil, float[] result)
        {
            Parallel.For(0, ny, j =>
            {
                for (int i = 0; i < nx; i++)
                {
                    float v = float.PositiveInfinity;
                    foreach (var (dx, dy) in stencil)
                    {
                        int x = i + dx, y = j + dy;
                        if (x >= 0 && y >= 0 && x < nx && y < ny)
                        {
                            v = MathF.Min(v, src[x + y * nx]);
                        }
                    }
                    result[i + j * nx] = v;
                }
            });
        }

        public static void DiskMax(float[] src, int nx, int ny, (int dx, int dy)[] stencil, float[] result)
        {
            Parallel.For(0, ny, j =>
            {
                for (int i = 0; i < nx; i++)
                {
                    float v = float.NegativeInfinity;
                    foreach (var (dx, dy) in stencil)
                    {
                        int x = i + dx, y = j + dy;
                        if (x >= 0 && y >= 0 && x < nx && y < ny)
                        {
                            v = MathF.Max(v, src[x + y * nx]);
                        }
                    }
                    result[i + j * nx] = v;
                }
            });
        }
    }
}
