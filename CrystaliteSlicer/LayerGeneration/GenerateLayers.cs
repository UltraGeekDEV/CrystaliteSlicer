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
        (int minZ, int maxZ)[,] height;
        float[,] maxHeight;
        (int minZ, int maxZ)[,] nextHeight;

        float zVoxelsPerX;
        int curLayer;
        const float diagonalConstant = 0.3f;

        public void GetLayers(IVoxelCollection voxels)
        {
            this.voxels = voxels;
            nozzleSize = new Vector3Int((new Vector3(Settings.NozzleDiameter, Settings.NozzleDiameter, 0) / Settings.Resolution) * (1.0f - Settings.OverhangOverlap));
            maxLayerThickness = Math.Max((int)(Settings.MaxLayerHeight / Settings.Resolution.Z), 1);

            height = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];

            zVoxelsPerX = (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X / Settings.Resolution.Z);
            curLayer = 1;

            List<Vector3Int> activeEdge = new List<Vector3Int>();
            object activeEdgeLock = new object();

            var firstLayer = Math.Min(maxLayerThickness, voxels.Size.Z);
            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() =>
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    for (int z = 0; z < firstLayer; z++)
                    {
                        if (voxels[x, y, z].Depth != -1)
                        {
                            var voxel = voxels[x, y, z];
                            voxel.Layer = 1;
                            voxels[x, y, z] = voxel;
                            height[x, y] = (Math.Min(height[x, y].maxZ, z + 1), Math.Max(height[x, y].minZ, z + 1));
                            lock (activeEdgeLock)
                            {
                                activeEdge.Add(new Vector3Int(x, y, z));
                            }
                        }
                    }
                }
            }));
            Task.WaitAll(tasks.ToArray());

            while (activeEdge.Count > 0)
            {
                curLayer++;

                maxHeight = new float[voxels.Size.X, voxels.Size.Y];
                nextHeight = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];
                var checkHeight = new int[voxels.Size.X, voxels.Size.Y];
                var minHeight = new int[voxels.Size.X, voxels.Size.Y];
                var neighbours = new List<(int x, int y)>() {
                (-1,0),
                (0,1),
                (1,0),
                (0,-1),
                };

                var maxHeightY = new float[voxels.Size.X, voxels.Size.Y];
                var maxHeightX = new float[voxels.Size.X, voxels.Size.Y];

                var sqrNozzleSize = nozzleSize.X * nozzleSize.X;

                var CheckAttachedAction = (int x) =>
                {
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (height[x, y].maxZ != 0 && neighbours.Any(coord =>
                        {
                            var pos = new Vector3Int(coord.x + x, coord.y + y);
                            return voxels.WithinBounds(pos) && height[pos.X, pos.Y].maxZ == 0;
                        }))
                        {
                            for (int i = -nozzleSize.X; i <= nozzleSize.X; i++)
                            {
                                for (int j = -nozzleSize.Y; j <= nozzleSize.Y; j++)
                                {
                                    var check = new Vector3Int(x+i, y+j, 0);
                                    if (voxels.WithinBounds(check) && height[check.X,check.Y].maxZ == 0 && (i*i+j*j) <= sqrNozzleSize)
                                    {
                                        var fromZ = height[x,y].minZ;
                                        var toZ = fromZ + maxLayerThickness;

                                        for (int z = fromZ; z <= toZ; z++)
                                        {
                                            check.Z = z;
                                            if (voxels.Contains(check) && voxels[check].Layer == 0)
                                            {
                                                if (checkHeight[check.X, check.Y] == 0)
                                                {
                                                    checkHeight[check.X, check.Y] = z;
                                                    minHeight[check.X, check.Y] = z;
                                                }
                                                else
                                                {
                                                    checkHeight[check.X, check.Y] = Math.Max(checkHeight[check.X, check.Y], z);
                                                    minHeight[check.X, check.Y] = Math.Min(minHeight[check.X, check.Y], z);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                var chunkSize = nozzleSize.X * 2 + 2;
                int threadCount = voxels.Size.X / chunkSize;

                for (int i = 0; i < chunkSize; i++)
                {
                    tasks = Enumerable.Range(0, threadCount).Where(x => x * chunkSize + i < voxels.Size.X).Select(x => Task.Run(() => CheckAttachedAction(x * chunkSize + i)));
                    Task.WaitAll(tasks.ToArray());
                }

                tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() =>
                {
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (height[x,y].maxZ != 0)
                        {
                            checkHeight[x,y] = height[x,y].maxZ;
                        }
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Get max height
                //loop y +-
                tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
                {
                    float curMaxHeight = voxels.Size.Z * 2;
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (checkHeight[x, y] != 0)
                        {
                            float curMaxZ = checkHeight[x, y] + maxLayerThickness;
                            curMaxHeight = Math.Min(curMaxZ, curMaxHeight + zVoxelsPerX);
                        }
                        else
                        {
                            curMaxHeight += zVoxelsPerX;
                        }
                        maxHeightY[x, y] = curMaxHeight;
                    }
                    curMaxHeight = maxHeightY[x, voxels.Size.Y - 1];
                    for (int y = voxels.Size.Y - 2; y >= 0; y--)
                    {
                        curMaxHeight = Math.Min(maxHeightY[x, y], curMaxHeight + zVoxelsPerX);
                        maxHeightY[x, y] = curMaxHeight;
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Loop x +-

                tasks = Enumerable.Range(0, voxels.Size.Y).AsParallel().Select(y => Task.Run(() =>
                {
                    float curMaxHeightY = maxHeightY[0, y];
                    for (int x = 1; x < voxels.Size.X; x++)
                    {
                        curMaxHeightY = Math.Min(maxHeightY[x, y], curMaxHeightY + (zVoxelsPerX*diagonalConstant));
                        maxHeightY[x, y] = curMaxHeightY;
                    }
                    curMaxHeightY = maxHeightY[voxels.Size.X - 1, y];
                    for (int x = voxels.Size.X - 2; x >= 0; x--)
                    {
                        curMaxHeightY = Math.Min(maxHeightY[x, y], curMaxHeightY + (zVoxelsPerX * diagonalConstant));
                        maxHeightY[x, y] = curMaxHeightY;
                    }

                    //maxHeight X pass
                    float curMaxHeight = voxels.Size.Z * 2;
                    for (int x = 0; x < voxels.Size.X; x++)
                    {
                        if (checkHeight[x, y] != 0)
                        {
                            float curMaxZ = checkHeight[x, y] + maxLayerThickness;
                            curMaxHeight = Math.Min(curMaxZ, curMaxHeight + zVoxelsPerX);
                        }
                        else
                        {
                            curMaxHeight += zVoxelsPerX;
                        }
                        maxHeightX[x, y] = curMaxHeight;
                    }
                    curMaxHeight = maxHeightX[voxels.Size.X - 1, y];
                    for (int x = voxels.Size.X - 2; x >= 0; x--)
                    {
                        curMaxHeight = Math.Min(maxHeightX[x, y], curMaxHeight + zVoxelsPerX);
                        maxHeightX[x, y] = curMaxHeight;
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Get max height
                //2nd loop y +-
                tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
                {
                    float curMaxHeight = maxHeightX[x, 0];
                    for (int y = 1; y < voxels.Size.Y; y++)
                    {
                        curMaxHeight = Math.Min(maxHeightX[x, y], curMaxHeight + (zVoxelsPerX * diagonalConstant));
                        maxHeightX[x, y] = curMaxHeight;
                    }
                    curMaxHeight = maxHeightX[x, voxels.Size.Y - 1];
                    for (int y = voxels.Size.Y - 2; y >= 0; y--)
                    {
                        curMaxHeight = Math.Min(maxHeightX[x, y], curMaxHeight + (zVoxelsPerX * diagonalConstant));
                        maxHeightX[x, y] = curMaxHeight;
                        maxHeight[x, y] = Math.Max(maxHeightX[x, y], maxHeightY[x, y]);
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Get next layer
                //var nextLayerVoxels = activeEdge.AsParallel().SelectMany(x => GetAttached(x)).Distinct().ToList();
                //nextLayerVoxels.AsParallel().ForAll(x =>
                //{
                //    var voxel = voxels[x];
                //    voxel.layer = curLayer;
                //    voxels[x] = voxel;
                //});

                var results = new List<Vector3Int>[voxels.Size.X];

                tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() =>
                {
                    var ret = new List<Vector3Int>();
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (checkHeight[x,y] != 0)
                        {
                            for (int z = minHeight[x,y]; z <= maxHeight[x,y] && z < voxels.Size.Z; z++)
                            {
                                var check = new Vector3Int(x, y, z);
                                var voxelData = voxels[check];
                                if (voxelData.Layer == 0 && voxelData.Depth != -1)
                                {
                                    ret.Add(check);
                                    var voxel = voxels[x,y,z];
                                    voxel.Layer = curLayer;
                                    voxels[x,y,z] = voxel;
                                }
                            }
                        }
                    }
                    results[x] = ret;
                }));
                Task.WaitAll(tasks.ToArray());

                var nextLayerVoxels = results.AsParallel().SelectMany(x=>x).ToList();

                var stillOpen = activeEdge.AsParallel().Where(HasOpenFace);

                var allActiveVoxels = stillOpen.AsParallel().Union(nextLayerVoxels.AsParallel().Where(HasOpenFace));
                var groupedEdge = allActiveVoxels.GroupBy(x => new Vector3Int(x.X, x.Y)).ToList();
                groupedEdge.AsParallel().ForAll(x =>
                {
                    nextHeight[x.Key.X, x.Key.Y] = (x.Min(x => x.Z), x.Max(x => x.Z));
                });
                activeEdge = groupedEdge.Select(x => new Vector3Int(x.Key.X, x.Key.Y, nextHeight[x.Key.X, x.Key.Y].minZ)).ToList();
                height = nextHeight;
            }
            voxels.LayerCount = curLayer;
        }

        private bool HasOpenFace(Vector3Int x)
        {
            for (int i = 0; i < LUTS.faceOffsets.Count; i++)
            {
                var check = x + LUTS.faceOffsets[i];
                if (voxels.Contains(check) && voxels[check].Layer == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private IEnumerable<Vector3Int> GetAttached(Vector3Int pos)
        {
            var ret = new List<Vector3Int>();

            for (int x = -nozzleSize.X; x <= nozzleSize.X; x++)
            {
                for (int y = -nozzleSize.Y; y <= nozzleSize.Y; y++)
                {
                    var offset = new Vector3Int(x, y, 0);
                    var check = offset + pos;
                    if (offset.Magnitude() <= nozzleSize.X && voxels.WithinBounds(check))
                    {
                        var toZ = maxHeight[check.X, check.Y];
                        int fromZ = pos.Z;

                        for (int z = fromZ; z <= toZ; z++)
                        {
                            check.Z = z;
                            if (voxels.Contains(check) && voxels[check].Layer == 0)
                            {
                                ret.Add(check);
                            }
                        }
                    }
                }
            }
            return ret;
        }
        private IEnumerable<Vector3Int> CheckAttached(Vector3Int pos)
        {
            var ret = new List<Vector3Int>();

            for (int x = -nozzleSize.X; x <= nozzleSize.X; x++)
            {
                for (int y = -nozzleSize.Y; y <= nozzleSize.Y; y++)
                {
                    var offset = new Vector3Int(x, y, 0);
                    var check = offset + pos;
                    if (offset.Magnitude() <= nozzleSize.X && voxels.WithinBounds(check))
                    {
                        int toZ;
                        int fromZ;
                        if (height[check.X, check.Y].maxZ == 0)
                        {
                            fromZ = pos.Z;
                        }
                        else
                        {
                            fromZ = height[check.X, check.Y].maxZ;
                        }

                        toZ = pos.Z + maxLayerThickness;

                        for (int z = fromZ; z <= toZ; z++)
                        {
                            check.Z = z;
                            if (voxels.Contains(check) && voxels[check].Layer == 0)
                            {
                                ret.Add(check);
                            }
                        }
                    }
                }
            }
            return ret;
        }
    }
}