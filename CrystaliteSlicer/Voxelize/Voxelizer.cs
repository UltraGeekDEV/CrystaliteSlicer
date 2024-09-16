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
        const int fillVoxelValue = 0;
        const int shellVoxelValue = 1;
        public IVoxelCollection Voxelize(IEnumerable<Triangle> triangles)
        {
            var startTime = DateTime.Now;
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

            upperRight += Vector3Int.One*2;

            mesh.AsParallel().ForAll(x =>
            {
                x.A = x.A - lowerLeft;
                x.B = x.B - lowerLeft;
                x.C = x.C - lowerLeft;
            });

            Console.WriteLine($"\tMesh Prep took:{(DateTime.Now - startTime).TotalMilliseconds} ms");
            startTime = DateTime.Now;

            IVoxelCollection voxels = new FlatVoxelArray(upperRight-lowerLeft);

            mesh.AsParallel().ForAll(x=>x.GetVoxelsBresenham(voxels));

            Console.WriteLine($"\tShell took:{(DateTime.Now - startTime).TotalMilliseconds}  ms");
            startTime = DateTime.Now;

            int seedSpacing = Math.Max(1, voxels.Size.X / Environment.ProcessorCount / 2);
            var seedPoints = Enumerable.Range(0, voxels.Size.X / seedSpacing).AsParallel().SelectMany(x =>
            {
                var ret = new List<Vector3Int>();
                for (int j = 0; j < voxels.Size.Y; j+= seedSpacing)
                {
                    Vector3Int check = new Vector3Int(x * seedSpacing, j, 0);
                    if (voxels[check].depth != shellVoxelValue)
                    {
                        ret.Add(check);
                        voxels[check] = new VoxelData() { depth = -1 };
                    }
                    check.Z = voxels.Size.Z - 1;
                    if (voxels[check].depth != shellVoxelValue)
                    {
                        ret.Add(check);
                        voxels[check] = new VoxelData() { depth = -1 };
                    }
                }
                return ret;
            });
            Console.WriteLine($"\tCorrosion seeding took:{(DateTime.Now - startTime).TotalMilliseconds} ms");
            startTime = DateTime.Now;

            var tasks = seedPoints.Select(x => Task.Run(() =>
            {
                Stack<Vector3Int> traversalQueue = new Stack<Vector3Int>();
                traversalQueue.Push(x);

                while (traversalQueue.Count > 0)
                {
                    var pos = traversalQueue.Pop();
                    var neighbours = GetNeighbours(pos, voxels);
                    for (int i = 0; i < neighbours.Count; i++)
                    {
                        traversalQueue.Push(neighbours[i]);
                    }
                }
            }));

            Task.WaitAll(tasks.ToArray());
            Console.WriteLine($"\tCorrosion took:{(DateTime.Now - startTime).TotalMilliseconds}  ms");

            var sdfTasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() =>
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    int dist = int.MinValue;
                    for (int z = 0; z < voxels.Size.Z; z++)
                    {
                        if (voxels[x, y, z].depth == -1)
                        {
                            dist = int.MinValue;
                        }
                        else
                        {
                            if (dist == int.MinValue)
                            {
                                dist = 0;
                            }
                            var voxel = voxels[x, y, z];
                            voxel.depth = dist++;
                            voxels[x, y, z] = voxel;
                        }
                    }
                    dist = int.MinValue;
                    for (int z = voxels.Size.Z - 1; z > 0; z--)
                    {
                        if (voxels[x, y, z].depth == -1)
                        {
                            dist = int.MinValue;
                        }
                        else
                        {
                            if (dist == int.MinValue)
                            {
                                dist = 0;
                            }
                            var voxel = voxels[x, y, z];
                            dist = Math.Min(dist, voxel.depth);
                            voxel.depth = dist;
                            dist++;
                            voxels[x, y, z] = voxel;
                        }
                    }
                }
            })).ToArray();

            Task.WaitAll(sdfTasks);

            return voxels;
        }
        
        private List<Vector3Int> GetNeighbours(Vector3Int check, IVoxelCollection voxels)
        {
            var ret = new List<Vector3Int>();
            var shellVoxelCount = 0;
            for (int i = 0; i < LUTS.faceOffsets.Count; i++)
            {
                var pos = check + LUTS.faceOffsets[i];
                if (voxels.WithinBounds(pos))
                {
                    var value = voxels[pos].depth;
                    if (value != -1 )
                    {
                        if (value != shellVoxelValue)
                        {
                            voxels[pos] = new VoxelData() { depth = -1 };
                            ret.Add(pos);
                        }
                        else
                        {
                            shellVoxelCount++;
                        }
                    }
                }
            }
            if (shellVoxelCount > 2)
            {
                ret.Clear();
            }
            return ret;
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
