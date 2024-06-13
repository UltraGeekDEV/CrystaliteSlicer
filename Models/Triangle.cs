using System.Numerics;

namespace Models
{
    public class Triangle
    {
        public Vector3 a, b, c;
        public Vector3Int A, B, C;

        public Triangle()
        {
        }
        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
        public Triangle(Vector3Int A, Vector3Int B, Vector3Int C)
        {
            this.A = A;
            this.B = B;
            this.C = C;
        }
        public Triangle Copy()
        {
            return new Triangle(a, b, c);
        }
        protected Vector3Int GetID(Vector3 vec, Vector3 resolution)
        {
            Vector3 scaled = new Vector3(
                vec.X / resolution.X,
                vec.Y / resolution.Y,
                vec.Z / resolution.Z
                );

            return new Vector3Int(
                (int)(scaled.X),
                (int)(scaled.Y),
                (int)(scaled.Z)
                );
        }
        protected Vector3 Id2Pos(Vector3Int id, Vector3 resolution)
        {
            return id * resolution;
        }

        public void Offset(Vector3 offset)
        {
            a += offset;
            b += offset;
            c += offset;
        }

        public void Scale(Vector3 scale)
        {
            a *= scale;
            b *= scale;
            c *= scale;
        }
        public void Rotate(Quaternion rot)
        {
            a = Vector3.Transform(a, rot);
            b = Vector3.Transform(b, rot);
            c = Vector3.Transform(c, rot);
        }
        public void GetVoxelsBresenham(IVoxelCollection voxels)
        {
            var edgeA = Bresenham3D(A, B);
            var edgeB = Bresenham3D(B, C);
            var edgeC = Bresenham3D(C, A);

            var edges = edgeA.Union(edgeB.Union(edgeC)).ToHashSet();

            var zGroup = edges.GroupBy(x => x.Z).Select(x => x.ToList()).ToList();

            for (int j = 0; j < zGroup.Count; j++)
            {
                for (int i = 0; i < zGroup[j].Count - 1; i++)
                {
                    var voxelLine = Bresenham3D(zGroup[j][i], zGroup[j][i + 1]);
                    for (int k = 0; k < voxelLine.Count; k++)
                    {
                        voxels[voxelLine[k]] = new VoxelData() { depth = 1 };
                    }
                }
            }

            var xGroup = edges.GroupBy(x => x.X).Select(x => x.ToList()).ToList();

            for (int j = 0; j < xGroup.Count; j++)
            {
                for (int i = 0; i < xGroup[j].Count - 1; i++)
                {
                    var voxelLine = Bresenham3D(xGroup[j][i], xGroup[j][i + 1]);
                    for (int k = 0; k < voxelLine.Count; k++)
                    {
                        voxels[voxelLine[k]] = new VoxelData() { depth = 1 };
                    }
                }
            }

            var yGroup = edges.GroupBy(x => x.Y).Select(x => x.ToList()).ToList();

            for (int j = 0; j < yGroup.Count; j++)
            {
                for (int i = 0; i < yGroup[j].Count - 1; i++)
                {
                    var voxelLine = Bresenham3D(yGroup[j][i], yGroup[j][i + 1]);
                    for (int k = 0; k < voxelLine.Count; k++)
                    {
                        voxels[voxelLine[k]] = new VoxelData() { depth = 1 };
                    }
                }
            }
        }

        public static List<Vector3Int> Bresenham3D(Vector3Int a, Vector3Int b)
        {
            List<Vector3Int> ListOfPoints = new List<Vector3Int>() {a};
            int dx = Math.Abs(b.X - a.X);
            int dy = Math.Abs(b.Y - a.Y);
            int dz = Math.Abs(b.Z - a.Z);
            int xs;
            int ys;
            int zs;
            if (b.X > a.X)
                xs = 1;
            else
                xs = -1;
            if (b.Y > a.Y)
                ys = 1;
            else
                ys = -1;
            if (b.Z > a.Z)
                zs = 1;
            else
                zs = -1;

            // Driving axis is X-axis"
            if (dx >= dy && dx >= dz)
            {
                int p1 = 2 * dy - dx;
                int p2 = 2 * dz - dx;
                while (a.X != b.X)
                {
                    a.X += xs;
                    if (p1 >= 0)
                    {
                        a.Y += ys;
                        p1 -= 2 * dx;
                    }
                    if (p2 >= 0)
                    {
                        a.Z += zs;
                        p2 -= 2 * dx;
                    }
                    p1 += 2 * dy;
                    p2 += 2 * dz;
                    ListOfPoints.Add(a);
                }

                // Driving axis is Y-axis"
            }
            else if (dy >= dx && dy >= dz)
            {
                int p1 = 2 * dx - dy;
                int p2 = 2 * dz - dy;
                while (a.Y != b.Y)
                {
                    a.Y += ys;
                    if (p1 >= 0)
                    {
                        a.X += xs;
                        p1 -= 2 * dy;
                    }
                    if (p2 >= 0)
                    {
                        a.Z += zs;
                        p2 -= 2 * dy;
                    }
                    p1 += 2 * dx;
                    p2 += 2 * dz;
                    ListOfPoints.Add(a);
                }

                // Driving axis is Z-axis"
            }
            else
            {
                int p1 = 2 * dy - dz;
                int p2 = 2 * dx - dz;
                while (a.Z != b.Z)
                {
                    a.Z += zs;
                    if (p1 >= 0)
                    {
                        a.Y += ys;
                        p1 -= 2 * dz;
                    }
                    if (p2 >= 0)
                    {
                        a.X += xs;
                        p2 -= 2 * dz;
                    }
                    p1 += 2 * dy;
                    p2 += 2 * dx;
                    ListOfPoints.Add(a);
                }
            }
            return ListOfPoints;
        }
    }
}
