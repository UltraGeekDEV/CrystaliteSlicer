using CrystaliteSlicer.MeshImport;
using Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Voxelize
{
    public class Voxelizer : IVoxelize
    {
        const int fillVoxelValue = int.MaxValue;
        const int shellVoxelValue = 1;
        public IVoxelCollection Voxelize(IEnumerable<Triangle> triangles)
        {
            var mesh = triangles.ToList();
            mesh.AsParallel().ForAll(x =>
            {
                x.A = Utils.GetID(x.a);
                x.B = Utils.GetID(x.b);
                x.C = Utils.GetID(x.c);
            });
            var lowerLeft = mesh.First().A;
            mesh.ForEach(x =>
            {
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.A, (a, b) => a > b);
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.B, (a, b) => a > b);
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.C, (a, b) => a > b);
            });
            var upperRight = mesh.First().A;
            mesh.ForEach(x =>
            {
                upperRight = Utils.CompareAndSwap(upperRight, x.A, (a, b) => a < b);
                upperRight = Utils.CompareAndSwap(upperRight, x.B, (a, b) => a < b);
                upperRight = Utils.CompareAndSwap(upperRight, x.C, (a, b) => a < b);
            });

            upperRight += Vector3Int.One;

            IVoxelCollection voxels = new FlatVoxelArray(upperRight-lowerLeft);

            mesh.AsParallel().SelectMany(x => x.GetVoxelsBresenham()).ForAll(x => voxels[x-lowerLeft] = new VoxelData() { depth = shellVoxelValue });

            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => FillVoxelMesh(voxels, x)));
            Task.WaitAll(tasks.ToArray());

            var corrosionBag = new ConcurrentQueue<Vector3Int>(voxels.GetAllActiveVoxels().AsParallel().Where(x => voxels[x].depth == fillVoxelValue && HasOpenFace(x, voxels)));
            corrosionBag.AsParallel().ForAll(x => voxels[x] = new VoxelData() { depth = -1 });
            tasks = Enumerable.Range(0, Environment.ProcessorCount).Select(x => Task.Run(() =>
            {
                while (!corrosionBag.IsEmpty)
                {
                    if (corrosionBag.TryDequeue(out var pos))
                    {
                        var neighbours = GetNeighbours(pos, voxels);
                        foreach (var check in neighbours)
                        {
                            voxels[check] = new VoxelData() { depth = -1 };
                            corrosionBag.Enqueue(check);
                        }
                    }
                }
            }));

            Task.WaitAll(tasks.ToArray());

            return voxels;
        }
        private bool HasOpenFace(Vector3Int check,IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Any(x => !voxels.Contains(x + check));
        }
        private IEnumerable<Vector3Int> GetNeighbours(Vector3Int check, IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Select(x => x+check).Where(x=>voxels.Contains(x) && voxels[x].depth == fillVoxelValue && LUTS.faceOffsets.Count(x => !voxels.Contains(x + check)) > 1);
        }
        private void FillVoxelMesh(IVoxelCollection voxels,int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                for (int z = 0; z < voxels.Size.Z; z++)
                {
                    if (voxels[x,y,z].depth != shellVoxelValue)
                    {
                        voxels[x, y, z] = new VoxelData() { depth = fillVoxelValue };
                    }
                }
            }
        }
    }
}
