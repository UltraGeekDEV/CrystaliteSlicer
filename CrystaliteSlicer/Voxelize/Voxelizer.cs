using CrystaliteSlicer.MeshImport;
using Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Voxelize
{
    public class Voxelizer : IVoxelize
    {
        
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
                upperRight = CompareAndSwap(upperRight, x.A, (a, b) => a < b);
                upperRight = CompareAndSwap(upperRight, x.B, (a, b) => a < b);
                upperRight = CompareAndSwap(upperRight, x.C, (a, b) => a < b);
            });

            upperRight += Vector3Int.One;

            mesh.AsParallel().ForAll(x =>
            {
                x.A = x.A - lowerLeft;
                x.B = x.B - lowerLeft;
                x.C = x.C - lowerLeft;
            });

            IVoxelCollection voxels = new FlatVoxelArray(upperRight-lowerLeft);

            mesh.AsParallel().ForAll(x => x.GetVoxelsBresenham(voxels));
            var seedPoints = mesh.Select(x=>(x.A + x.B + x.C) / 3 - x.GetNormal()*1).Where(x=>voxels.WithinBounds(x));
            
            var tasks = seedPoints.Select(x => Task.Run(() =>
            {
                var localQueue = new Queue<Vector3Int>();
                localQueue.Enqueue(x);
                if (voxels[x].depth != VoxelData.shellVoxelValue)
                {
                    voxels[x] = new VoxelData() { depth = VoxelData.fillVoxelValue };
                }
                //while (localQueue.Count > 0)
                //{
                //    var pos = localQueue.Dequeue();
                //    var neighbours = GetNeighbours(pos, voxels);
                //    foreach (var item in neighbours)
                //    {
                //        voxels[x] = new VoxelData() { depth = VoxelData.fillVoxelValue };

                //        if (!IsShellGap(item,voxels))
                //        {
                //            localQueue.Enqueue(pos);
                //        }
                //    }
                //}
            }));

            Task.WaitAll(tasks.ToArray());

            return voxels;
        }
        private bool HasOpenFace(Vector3Int check,IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Any(x => !voxels.Contains(x + check));
        }
        private bool IsShellGap(Vector3Int check, IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Count(x => voxels.Contains(x + check) && voxels[x + check].depth == VoxelData.shellVoxelValue) > 2;
        }
        private IEnumerable<Vector3Int> GetNeighbours(Vector3Int check, IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Select(x => x+check).Where(x=>voxels.WithinBounds(x) && voxels[x].depth == -1);
        }
        private void FillVoxelMesh(IVoxelCollection voxels,int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                for (int z = 0; z < voxels.Size.Z; z++)
                {
                    if (voxels[x,y,z].depth != 1)
                    {
                        voxels[x, y, z] = new VoxelData() { depth = VoxelData.fillVoxelValue };
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
            return pos / Settings.Resolution;
        }
    }
}
