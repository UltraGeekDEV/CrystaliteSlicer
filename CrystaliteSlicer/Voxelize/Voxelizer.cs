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
                upperRight = CompareAndSwap(upperRight, x.A, (a, b) => a < b);
                upperRight = CompareAndSwap(upperRight, x.B, (a, b) => a < b);
                upperRight = CompareAndSwap(upperRight, x.C, (a, b) => a < b);
            });

            upperRight += Vector3Int.One;

            IVoxelCollection voxels = new FlatVoxelArray(upperRight-lowerLeft);

            mesh.AsParallel().SelectMany(x => x.GetVoxelsBresenham()).ForAll(x => voxels[x-lowerLeft] = new VoxelData() { depth = shellVoxelValue });
            Console.WriteLine("\tStarted Fill");
            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x=>Task.Run(() => FillVoxelMesh(voxels,x)));
            Task.WaitAll(tasks.ToArray());
            bool[] taskDone = new bool[Environment.ProcessorCount];
            var corrosionQueue = new ConcurrentQueue<Vector3Int>();
            Console.WriteLine("\tStarted Corrosion Finding");
            tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() =>
            {
                for (int j = 0; j < voxels.Size.Y; j++)
                {
                    for (int z = 0; z < voxels.Size.Z; z++)
                    {
                        Vector3Int check = new Vector3Int(x, j, z);
                        if (HasOpenFace(check, voxels) && voxels[check].depth != shellVoxelValue)
                        {
                            corrosionQueue.Enqueue(check);
                            voxels[check] = new VoxelData() { depth = -1 };
                        }
                    }
                }
            }));

            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("\tStarted Corrosion");
            tasks = Enumerable.Range(0, Environment.ProcessorCount).Select(x => Task.Run(() =>
            {
                int id = x;
                taskDone[id] = false;
                Queue<Vector3Int> traversalQueue = new Queue<Vector3Int>();
                Queue<Vector3Int> toAddQueue = new Queue<Vector3Int>();
                while (!taskDone.All(x => x))
                {
                    while (!corrosionQueue.IsEmpty && traversalQueue.Count < 200)
                    {
                        if (corrosionQueue.TryDequeue(out var pos))
                        {
                            traversalQueue.Enqueue(pos);
                        }
                    }

                    while (traversalQueue.Count > 0)
                    {
                        var pos = traversalQueue.Dequeue();
                        var neighbours = GetNeighbours(pos, voxels);
                        foreach (var check in neighbours)
                        {
                            if (voxels[check].depth != shellVoxelValue && !IsShellGap(check,voxels))
                            {
                                voxels[check] = new VoxelData() { depth = -1 };
                                toAddQueue.Enqueue(check);
                            }
                        }
                    }

                    while (toAddQueue.Count > 0)
                    {
                        corrosionQueue.Enqueue(toAddQueue.Dequeue());
                    }

                    taskDone[id] = corrosionQueue.IsEmpty;
                }
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
            return LUTS.faceOffsets.Count(x => voxels.Contains(x + check) && voxels[x + check].depth == shellVoxelValue) > 2;
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
            return pos / Settings.Resolution;
        }
    }
}
