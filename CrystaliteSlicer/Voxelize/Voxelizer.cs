using CrystaliteSlicer.MeshImport;
using Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                x.A = GetID(x.a);
                x.B = GetID(x.b);
                x.C = GetID(x.c);
            });
            var lowerLeft = mesh.First().A;
            mesh.ForEach(x =>
            {
                lowerLeft = CompareAndSwap(lowerLeft, x.A, (a, b) => a > b);
                lowerLeft = CompareAndSwap(lowerLeft, x.B, (a, b) => a > b);
                lowerLeft = CompareAndSwap(lowerLeft, x.C, (a, b) => a > b);
            });
            var upperRight = mesh.First().A;
            mesh.ForEach(x =>
            {
                lowerLeft = CompareAndSwap(lowerLeft, x.A, (a, b) => a < b);
                lowerLeft = CompareAndSwap(lowerLeft, x.B, (a, b) => a < b);
                lowerLeft = CompareAndSwap(lowerLeft, x.C, (a, b) => a < b);
            });

            IVoxelCollection voxels = new FlatVoxelArray(upperRight-lowerLeft);

            mesh.AsParallel().SelectMany(x => x.GetVoxelsBresenham()).ForAll(x => voxels[x-lowerLeft] = new VoxelData() { depth = shellVoxelValue });

            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x=>Task.Run(() => FillVoxelMesh(voxels,x)));
            Task.WaitAll(tasks.ToArray());

            var corrosionBag = new ConcurrentBag<Vector3Int>(voxels.GetAllActiveVoxels().Where(x=> voxels[x].depth == fillVoxelValue && HasOpenFace(x,voxels)));
            foreach (var item in corrosionBag.ToList())
            {
                voxels[item] = new VoxelData() { depth = -1 };
            }
            tasks = Enumerable.Range(0, Environment.ProcessorCount).Select(x => Task.Run(() =>
            {
                while (!corrosionBag.IsEmpty)
                {
                    if(corrosionBag.TryTake(out var pos))
                    {
                        var neighbours = GetNeighbours(pos, voxels);
                        foreach (var check in neighbours)
                        {
                            voxels[check] = new VoxelData() { depth = -1 };
                            corrosionBag.Add(check);
                        }
                    }
                }
            }));

            return voxels;
        }
        private bool HasOpenFace(Vector3Int check,IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Any(x => !voxels.Contains(x + check));
        }
        private IEnumerable<Vector3Int> GetNeighbours(Vector3Int check, IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Select(x => x+check).Where(voxels.Contains);
        }
        private void FillVoxelMesh(IVoxelCollection voxels,int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                for (int z = 0; z < voxels.Size.Z; z++)
                {
                    if (voxels[x,y,z].depth != 1)
                    {
                        voxels[x, y, z] = new VoxelData() { depth = fillVoxelValue };
                    }
                }
            }
        }
        private Vector3Int CompareAndSwap(Vector3Int A,Vector3Int B,Func<int, int, bool> predicate)
        {
            if (predicate(A.X, B.X))
            {
                A.X = B.X;
            }
            if(predicate(A.Y, B.Y))
            { 
                A.Y = B.Y; 
            }
            if(predicate(A.Z, B.Z))
            {
                A.Z = B.Z;
            }
            return A;
        }
        private Vector3Int GetID(Vector3 pos)
        {
            return pos / Settings.Instance.Resolution;
        }
    }
}
