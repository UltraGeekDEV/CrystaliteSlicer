using Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.LayerGeneration
{
    public class GenerateLayers : IGenerateLayers
    {
        Vector3Int nozzleSize;
        int maxLayerThickness;
        IVoxelCollection voxels;

        int maxZ;
        int minZ;
        int[,] height;
        float[,] maxHeight;
        int[,] nextHeight;

        float zVoxelsPerX;
        int curLayer;
        public void GetLayers(IVoxelCollection voxels)
        {
            this.voxels = voxels;
            nozzleSize = new Vector3Int(new Vector3(Settings.NozzleDiameter, Settings.NozzleDiameter, 0) / Settings.Resolution);
            maxLayerThickness = (int)(Settings.MaxLayerHeight / Settings.Resolution.Z);

            height = new int[voxels.Size.X, voxels.Size.Y];

            zVoxelsPerX = (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X);
            curLayer = 1;

            var start = DateTime.Now;
            var firstLayer = Math.Min(maxLayerThickness, voxels.Size.Z);
            var tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    for (int z = 0; z < firstLayer; z++)
                    {
                        if (voxels[x, y, z].depth >= 0)
                        {
                            var voxel = voxels[x, y, z];
                            voxel.layer = 1;
                            voxels[x, y, z] = voxel;
                            height[x, y] = Math.Max(height[x, y], z);
                        }
                    }
                }
            }));
            Task.WaitAll(tasks.ToArray());

            Console.WriteLine($"\tFirst Layer Took:{(DateTime.Now - start).TotalMilliseconds}");
            start = DateTime.Now;

            maxZ = firstLayer;
            minZ = 0;

            int nextMaxZ = 0;
            int nextMinZ = voxels.Size.Z;
            object zBoundsLock = new object();
            maxHeight = new float[voxels.Size.X, voxels.Size.Y];
            nextHeight = new int[voxels.Size.X, voxels.Size.Y];
            Queue<Vector3Int> traversalQueue = new Queue<Vector3Int>();
            object queueLock = new object();

            tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
            {
                float curMaxHeight = voxels.Size.Z*2;
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    if (height[x,y] != 0)
                    {
                        int thickness = maxZ;
                        bool hasEntered = false;
                        while (thickness >= minZ && voxels[x, y, thickness].layer == 0)
                        {
                            thickness--;
                        }
                        float curMaxZ = thickness + maxLayerThickness;
                        if (thickness < minZ)
                        {
                            curMaxZ = voxels.Size.Z;
                        }

                        curMaxHeight = Math.Min(curMaxZ, curMaxHeight + zVoxelsPerX);
                    }
                    maxHeight[x, y] = curMaxHeight;
                }
                curMaxHeight = maxHeight[x, voxels.Size.Y - 1];
                for (int y = voxels.Size.Y-2; y >= 0; y--)
                {
                    curMaxHeight = Math.Min(maxHeight[x, y], curMaxHeight + zVoxelsPerX);
                    maxHeight[x, y] = curMaxHeight;
                }
            }));
            Task.WaitAll(tasks.ToArray());
            tasks = Enumerable.Range(0, voxels.Size.Y).AsParallel().Select(y => Task.Run(() =>
            {
                float curMaxHeight = maxHeight[0, y];
                for (int x = 1; x < voxels.Size.X; x++)
                {
                    curMaxHeight = Math.Min(maxHeight[x, y], curMaxHeight + zVoxelsPerX);
                    maxHeight[x, y] = curMaxHeight;
                }
                curMaxHeight = maxHeight[voxels.Size.X-1, y];
                for (int x = voxels.Size.X - 2; x >= 0; x--)
                {
                    curMaxHeight = Math.Min(maxHeight[x, y], curMaxHeight + zVoxelsPerX);
                    maxHeight[x, y] = curMaxHeight;
                }
            }));
            Task.WaitAll(tasks.ToArray());

            Console.WriteLine($"\tMaxMap Took:{(DateTime.Now - start).TotalMilliseconds}");
            start = DateTime.Now;

            tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
            {
                int bbMax = nextMaxZ;
                int bbMin = nextMinZ;
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    int seedZ = bbMax;

                    for (int z = minZ; z < maxZ; z++)
                    {
                        if (voxels.Contains(new Vector3Int(x, y, z)))
                        {
                            seedZ = Math.Min(seedZ, z);
                        }
                    }
                    if (voxels[x, y, seedZ].depth >= 0)
                    {
                        int min, max;
                        AddAttached(new Vector3Int(x, y, seedZ), out min, out max);

                        bbMin = Math.Min(min, bbMin);
                        bbMax = Math.Max(max, bbMax);
                        nextHeight[x, y] = max;
                    }
                }
                lock (zBoundsLock)
                {
                    nextMaxZ = Math.Max(nextMaxZ, bbMax);
                    nextMinZ = Math.Min(nextMinZ, bbMin);
                }
            }));
            Task.WaitAll(tasks.ToArray());
            height = nextHeight;
            Console.WriteLine($"\tSecond Layer Took:{(DateTime.Now - start).TotalMilliseconds}");
            start = DateTime.Now;
        }

        private void AddAttached(Vector3Int pos,out int minZ,out int maxZ)
        {
            int min = int.MaxValue;
            int max = int.MinValue;

            for (int x = -nozzleSize.X; x <= nozzleSize.X; x++)
            {
                for (int y = -nozzleSize.Y; y <= nozzleSize.Y; y++)
                {
                    var check = new Vector3Int(x, y, 0) + pos;
                    if (voxels.WithinBounds(check))
                    {
                        var toZ = maxHeight[x,y];
                        int fromZ;
                        if (height[check.X,check.Y] == 0)
                        {
                            fromZ = pos.Z;
                        }
                        else
                        {
                            fromZ = height[check.X, check.Y];
                        }

                        for (int z = fromZ; z < toZ; z++)
                        {
                            check.Z = z;
                            if (voxels.WithinBounds(check) && voxels[check].layer == 0)
                            {
                                min = Math.Min(min, z);
                                max = Math.Max(max, z);
                                var voxel = voxels[check];
                                voxel.layer = curLayer;
                                voxels[check] = voxel;
                            }
                        }
                    }
                }
            }
            minZ = min;
            maxZ = max;
        }

        private int GetMaxHeight(Vector3Int pos)
        {
            int maxHeight = maxLayerThickness;
            for (int x = 0; x < voxels.Size.X; x++)
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    if (height[x,y] != 0)
                    {
                        if (height[pos.X, pos.Y] == 0)
                        {
                            maxHeight = Math.Min(maxHeight, (int)(Math.Sqrt(Math.Pow(x - pos.X, 2) + Math.Pow(y - pos.Y, 2)) * zVoxelsPerX - pos.Z + height[x, y] + maxLayerThickness));
                        }
                        else
                        {
                            maxHeight = Math.Min(maxHeight, (int)(Math.Sqrt(Math.Pow(x - pos.X, 2) + Math.Pow(y - pos.Y, 2)) * zVoxelsPerX - height[pos.X, pos.Y] + height[x, y] + maxLayerThickness));
                        }
                    }
                }
            }
            return maxHeight;
        }
    }
}
