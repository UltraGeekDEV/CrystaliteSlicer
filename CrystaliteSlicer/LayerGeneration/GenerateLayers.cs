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
        public void GetLayers(IVoxelCollection voxels)
        {
            this.voxels = voxels;
            nozzleSize = new Vector3Int(new Vector3(Settings.NozzleDiameter, Settings.NozzleDiameter, 0) / Settings.Resolution);
            maxLayerThickness = Math.Max((int)(Settings.MaxLayerHeight / Settings.Resolution.Z), 1);

            height = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];

            zVoxelsPerX = (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X/ Settings.Resolution.Z);
            curLayer = 1;

            List<Vector3Int> activeEdge = new List<Vector3Int>();
            object activeEdgeLock = new object();

            var start = DateTime.Now;
            var firstLayer = Math.Min(maxLayerThickness, voxels.Size.Z);
            var tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    for (int z = 0; z < firstLayer; z++)
                    {
                        if (voxels[x, y, z].depth != -1)
                        {
                            var voxel = voxels[x, y, z];
                            voxel.layer = 1;
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

            Console.WriteLine($"\tFirst Layer Took:{(DateTime.Now - start).TotalMilliseconds} ms");
            start = DateTime.Now;

            maxZ = firstLayer;
            minZ = 0;

            bool addedVoxel = true;

            while (activeEdge.Count > 0)
            {
                curLayer++;

                maxHeight = new float[voxels.Size.X, voxels.Size.Y];
                nextHeight = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];
                var checkHeight = new int[voxels.Size.X, voxels.Size.Y];
                activeEdge.AsParallel().SelectMany(CheckAttached).Distinct().GroupBy(x => new Vector3Int(x.X, x.Y)).ForAll(x =>
                {
                    checkHeight[x.Key.X, x.Key.Y] = Math.Max(x.Min(x => x.Z), height[x.Key.X,x.Key.Y].maxZ);
                });
                //Get max height
                //loop y +-
                tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
                {
                    float curMaxHeight = voxels.Size.Z * 2;
                    int startZ = 0;
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
                        maxHeight[x, y] = curMaxHeight;
                    }
                    curMaxHeight = maxHeight[x, voxels.Size.Y - 1];
                    for (int y = voxels.Size.Y - 2; y >= 0; y--)
                    {
                        curMaxHeight = Math.Min(maxHeight[x, y], curMaxHeight + zVoxelsPerX);
                        maxHeight[x, y] = curMaxHeight;
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Loop x +-

                tasks = Enumerable.Range(0, voxels.Size.Y).AsParallel().Select(y => Task.Run(() =>
                {
                    float curMaxHeight = maxHeight[0, y];
                    for (int x = 1; x < voxels.Size.X; x++)
                    {
                        curMaxHeight = Math.Min(maxHeight[x, y], curMaxHeight + zVoxelsPerX);
                        maxHeight[x, y] = curMaxHeight;
                    }
                    curMaxHeight = maxHeight[voxels.Size.X - 1, y];
                    for (int x = voxels.Size.X - 2; x >= 0; x--)
                    {
                        curMaxHeight = Math.Min(maxHeight[x, y], curMaxHeight + zVoxelsPerX);
                        maxHeight[x, y] = curMaxHeight;
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Get next layer
                var nextLayerVoxels = activeEdge.AsParallel().SelectMany(x => GetAttached(x)).Distinct().ToList();
                nextLayerVoxels.AsParallel().ForAll(x =>
                {
                    var voxel = voxels[x];
                    voxel.layer = curLayer;
                    voxels[x] = voxel;
                });
                var stillOpen = activeEdge.AsParallel().Where(HasOpenFace);

                var allActiveVoxels = stillOpen.AsParallel().Union(nextLayerVoxels.AsParallel().Where(HasOpenFace));
                var groupedEdge = allActiveVoxels.GroupBy(x => new Vector3Int(x.X, x.Y)).ToList();
                groupedEdge.AsParallel().ForAll(x =>
                {
                    nextHeight[x.Key.X, x.Key.Y] = (x.Min(x => x.Z), x.Max(x => x.Z));
                });
                activeEdge = groupedEdge.Select(x => new Vector3Int(x.Key.X, x.Key.Y, nextHeight[x.Key.X, x.Key.Y].minZ)).ToList();
                height = nextHeight;
                Console.WriteLine($"\tFinnished layer {curLayer}");
            }
            //ExportMaxHeightInstead

            //for (int x = 0; x < voxels.Size.X; x++)
            //{
            //    for (int y = 0; y < voxels.Size.Y; y++)
            //    {
            //        for (int z = 0; z < maxHeight[x, y]; z++)
            //        {
            //            voxels[x, y, z] = new VoxelData() { depth = 1, layer = 1 };
            //        }
            //    }
            //}

            //ExportHeightInstead

            //for (int x = 0; x < voxels.Size.X; x++)
            //{
            //    for (int y = 0; y < voxels.Size.Y; y++)
            //    {
            //        for (int z = 0; z < height[x, y]; z++)
            //        {
            //            voxels[x, y, z] = new VoxelData() { depth = 1, layer = 1 };
            //        }
            //    }
            //}
            var time = (DateTime.Now - start).TotalMilliseconds;
            Console.WriteLine($"\tNonPlanar Layers Took:{time} ms");
            Console.WriteLine($"\t\tOn avarage a layer took:{time/(curLayer-1)} ms");
            start = DateTime.Now;
        }

        private bool HasOpenFace(Vector3Int x)
        {
            for (int i = 0; i < LUTS.faceOffsets.Count; i++)
            {
                var check = x + LUTS.faceOffsets[i];
                if (voxels.Contains(check) && voxels[check].layer == 0)
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
                    var check = new Vector3Int(x, y, 0) + pos;
                    if (voxels.WithinBounds(check))
                    {
                        var toZ = maxHeight[check.X, check.Y];
                        int fromZ = pos.Z;
                        //if (height[check.X, check.Y].maxZ == 0)
                        //{
                        //    fromZ = pos.Z;
                        //}
                        //else
                        //{
                        //    fromZ = height[check.X, check.Y].maxZ;
                        //}


                        for (int z = fromZ; z <= toZ; z++)
                        {
                            check.Z = z;
                            if (voxels.Contains(check) && voxels[check].layer == 0)
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
                    var check = new Vector3Int(x, y, 0) + pos;
                    if (voxels.WithinBounds(check))
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
                            if (voxels.Contains(check) && voxels[check].layer == 0)
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
