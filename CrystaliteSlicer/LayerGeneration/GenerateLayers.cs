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

            int[,] activeEdge = new int[voxels.Size.X,voxels.Size.Y];
            int activeVoxels = 0;

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
                            height[x, y] = (Math.Min(height[x, y].minZ, z + 1), Math.Max(height[x, y].maxZ, z + 1));
                        }
                    }
                    if (height[x,y].maxZ == 0)
                    {
                        activeEdge[x, y] = -1;
                    }
                    else
                    {
                        activeVoxels++;
                    }
                }
            }));
            Task.WaitAll(tasks.ToArray());

            Console.WriteLine($"\tFirst Layer Took:{(DateTime.Now - start).TotalMilliseconds} ms");
            start = DateTime.Now;

            maxZ = firstLayer;
            minZ = 0;

            bool addedVoxel = true;
            int layerCount = 50;
            int[,] checkHeight  = null;
            while (activeVoxels > 0 && layerCount > 0)
            {
                layerCount--;
                activeVoxels = 0;
                curLayer++;

                maxHeight = new float[voxels.Size.X, voxels.Size.Y];
                nextHeight = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];
                checkHeight = new int[voxels.Size.X, voxels.Size.Y];
                //activeEdge.AsParallel().SelectMany(CheckAttached).Distinct().GroupBy(x => new Vector3Int(x.X, x.Y)).ForAll(x =>
                //{
                //    checkHeight[x.Key.X, x.Key.Y] = Math.Max(x.Min(x => x.Z), height[x.Key.X,x.Key.Y].maxZ);
                //});
                //Get distances from previous edge
                tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
                {
                    int curDist = 0;
                    int curHeight = int.MaxValue;
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (height[x, y].minZ != 0)
                        {
                            curHeight = height[x, y].minZ;
                        }
                        checkHeight[x, y] = curHeight;

                        if (activeEdge[x,y] != -1)
                        {
                            curDist = Math.Min(activeEdge[x, y], curDist + 1);
                        }
                        else
                        {
                            curDist += 1;
                        }
                        activeEdge[x, y] = curDist;
                    }
                    curDist = voxels.Size.X;
                    curHeight = int.MaxValue;
                    for (int y = voxels.Size.Y - 1; y >= 0; y--)
                    {
                        if (checkHeight[x, y] != int.MaxValue)
                        {
                            curHeight = checkHeight[x, y];
                        }
                        else
                        {
                            checkHeight[x, y] = curHeight;
                        }

                        curDist = Math.Min(activeEdge[x, y], curDist + 1);
                        activeEdge[x, y] = curDist;
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //Loop x +-

                tasks = Enumerable.Range(0, voxels.Size.Y).AsParallel().Select(y => Task.Run(() =>
                {
                    int curDist = voxels.Size.X;
                    int curHeight = int.MaxValue;
                    for (int x = 0; x < voxels.Size.X; x++)
                    {
                        curHeight = Math.Min(curHeight, checkHeight[x, y]);
                        checkHeight[x, y] = curHeight;

                        curDist = Math.Min(activeEdge[x, y], curDist+1);
                        activeEdge[x, y] = curDist;
                    }
                    curDist = voxels.Size.X;
                    curHeight = int.MaxValue;
                    for (int x = voxels.Size.X - 1; x >= 0; x--)
                    {
                        curHeight = Math.Min(curHeight, checkHeight[x, y]);
                        checkHeight[x, y] = curHeight;

                        curDist = Math.Min(activeEdge[x, y], curDist + 1);
                        activeEdge[x, y] = curDist;

                        if (activeEdge[x, y] > nozzleSize.X)
                        {
                            checkHeight[x, y] = 0;
                            activeEdge[x, y] = -1;
                        }
                        else
                        {
                            if (height[x,y].maxZ != 0)
                            {
                                checkHeight[x,y] = height[x,y].maxZ;
                            }
                            var maxZCheck = checkHeight[x, y] + maxLayerThickness;
                            int firstActive = -1;
                            for (int i = checkHeight[x, y]; i < maxZCheck; i++)
                            {
                                if (voxels.Contains(new Vector3Int(x,y,i)) && voxels[x, y, i].layer == 0)
                                {
                                    firstActive = i;
                                }
                            }
                            if(firstActive != -1)
                            {
                                checkHeight[x, y] = Math.Max(firstActive, height[x, y].maxZ);
                            }
                            else
                            {
                                checkHeight[x, y] = 0;
                            }
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
                if (layerCount == 2)
                {
                    break;
                }
                //Get next layer
                var nozzleSqr = nozzleSize.X* nozzleSize.X;
                tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
                {
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (activeEdge[x,y] != -1)
                        {
                            int min = int.MaxValue;
                            int max = int.MinValue;
                            for (int z = checkHeight[x,y]; z < maxHeight[x, y]; z++)
                            {
                                if (voxels.Contains(new Vector3Int(x, y, z)) && voxels[x, y, z].layer == 0)
                                {
                                    var voxel = voxels[x, y, z];
                                    voxel.layer = curLayer;
                                    voxels[x, y, z] = voxel;
                                    min = Math.Min(min, z);
                                    max = Math.Max(max, z);
                                }
                            }
                            if (min != int.MaxValue)
                            {
                                nextHeight[x, y] = (min, max);
                                activeVoxels++;
                            }
                            else
                            {
                                nextHeight[x, y] = default;
                            }
                        }
                        else
                        {
                            nextHeight[x, y] = default;
                        }
                    }
                }));
                Task.WaitAll(tasks.ToArray());

                //var nextLayerVoxels = activeEdge.AsParallel().SelectMany(x => GetAttached(x)).Distinct().ToList();
                //nextLayerVoxels.AsParallel().ForAll(x =>
                //{
                //    var voxel = voxels[x];
                //    voxel.layer = curLayer;
                //    voxels[x] = voxel;
                //});

                //tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
                //{
                //    for (int y = 0; y < voxels.Size.Y; y++)
                //    {
                //        if (activeEdge[x, y] < nozzleSize.X)
                //        {
                //            int min = int.MaxValue;
                //            int max = int.MinValue;
                //            for (int z = checkHeight[x, y]; z < maxHeight[x, y]; z++)
                //            {
                //                if(HasOpenFace(new Vector3Int(x, y, z)))
                //                {
                //                    min = Math.Min(min, z);
                //                    max = Math.Max(max, z);
                //                }
                //            }
                //            if (min != int.MaxValue)
                //            {
                //                activeEdge[x, y] = 0;
                //                nextHeight[x,y] = (min, max);
                //                activeVoxels++;
                //            }
                //            else
                //            {
                //                activeEdge[x, y] = -1;
                //            }
                //        }
                //        else
                //        {
                //            activeEdge[x, y] = -1;
                //        }
                //    }
                //}));
                //Task.WaitAll(tasks.ToArray());

                height = nextHeight;
                Console.WriteLine($"\tFinnished layer {curLayer}");
            }
            //ExportMaxHeightInstead

            for (int x = 0; x < voxels.Size.X; x++)
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    for (int z = 0; z < voxels.Size.Z; z++)
                    {
                        voxels[x, y, z] = new VoxelData() { depth = -1, layer = 0 };
                    }
                    for (int z = height[x,y].minZ; z < height[x, y].maxZ; z++)
                    {
                        voxels[x, y, z] = new VoxelData() { depth = 1, layer = 1 };
                    }
                }
            }

            //ExportHeightInstead

            //for (int x = 0; x < voxels.Size.X; x++)
            //{
            //    for (int y = 0; y < voxels.Size.Y; y++)
            //    {
            //        for (int z = 0; z < height[x, y].maxZ; z++)
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
