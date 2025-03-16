using Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        List<Vector3Int> activeEdge = new List<Vector3Int>();
        public void GetLayers(IVoxelCollection voxels)
        {
            this.voxels = voxels;
            nozzleSize = new Vector3Int((new Vector3(Settings.NozzleDiameter, Settings.NozzleDiameter, 0) / Settings.Resolution) * (1.0f - Settings.OverhangOverlap));
            maxLayerThickness = Math.Max((int)(Settings.MaxLayerHeight / Settings.Resolution.Z), 1);

            height = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];

            zVoxelsPerX = (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X / Settings.Resolution.Z);
            curLayer = 1;
            var start = DateTime.Now;

            GetFirstLayer();

            Debug.WriteLine($"\tFirst Layer Took:{(DateTime.Now - start).TotalMilliseconds}");
            start = DateTime.Now;

            GetNonPlanarLayers();

            Debug.WriteLine($"\tNon-Planar Layers Took:{(DateTime.Now - start).TotalMilliseconds}");
            start = DateTime.Now;

            Debug.WriteLine($"\tNonPlanar Layers Took:{(DateTime.Now - start).TotalMilliseconds}");
            
            voxels.LayerCount = curLayer;
        }
        private double MeasuredRunMillis(Action a)
        {
            var start = DateTime.Now;
            a();
            return (DateTime.Now - start).TotalMilliseconds;
        }
        //################## NON_PLANAR LAYERS ##################
        private void GetNonPlanarLayers()
        {
            double checkHeightTime = 0;
            double maxHeightTime = 0;
            double nextLayerTime = 0;

            while (activeEdge.Count > 0)
            {
                curLayer++;

                maxHeight = new float[voxels.Size.X, voxels.Size.Y];
                nextHeight = new (int minZ, int maxZ)[voxels.Size.X, voxels.Size.Y];
                var checkHeight = new int[voxels.Size.X, voxels.Size.Y];

                //################## CHECK HEIGHT ##################

                checkHeightTime += MeasuredRunMillis(() => InitCheckHeight(checkHeight));

                //################## MAX HEIGHT ##################
                maxHeightTime += MeasuredRunMillis(()=> GetMaxHeight(checkHeight));

                //################## NEXT LAYER ##################
                nextLayerTime += MeasuredRunMillis(GetNextLayer);

                Debug.WriteLine($"\tFinnished layer {curLayer} with {activeEdge.Count} voxels in active edge");
            }

            var totalTime = checkHeightTime + maxHeightTime + nextLayerTime;
            Debug.WriteLine($"\tNon-Planar layer-gen took: {totalTime/1000} s\n\tWith a ratio of:\n\t\tCheck Height: {checkHeightTime/totalTime*100} %" +
                $"\n\t\tMax Height: {maxHeightTime/totalTime*100} %\n\t\tNext Layer: {nextLayerTime/totalTime*100}");
        }


        private void InitCheckHeight(int[,] checkHeight)
        {
            var traversalLUT = new List<Vector3Int>();
            //Get the offsets that are valid for a circle
            for (int x = -nozzleSize.X; x <= nozzleSize.X; x++)
            {
                for (int y = -nozzleSize.Y; y <= nozzleSize.Y; y++)
                {
                    var offset = new Vector3Int(x, y, 0);
                    if (offset.Magnitude() <= nozzleSize.X)
                    {
                        traversalLUT.Add(offset);
                    }
                }
            }

            var activeCheck = new int[voxels.Size.X, voxels.Size.Y];

            foreach(var voxel in activeEdge.AsParallel())
            {
                activeCheck[voxel.X, voxel.Y] = voxel.Z;
            }

            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() =>
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    var neighbours = traversalLUT.Select(offset => offset + new Vector3Int(x, y)).Where(check => voxels.WithinBounds(check) && activeCheck[check.X, check.Y] != 0).ToList();
                    if (neighbours.Count > 0)
                    {
                        int fromZ;

                        if (height[x,y].maxZ == 0)
                        {
                            fromZ = neighbours.Min(check => activeCheck[check.X, check.Y]);
                        }
                        else
                        {
                            fromZ = height[x, y].maxZ;
                        }

                        var toZ = neighbours.Max(check => activeCheck[check.X, check.Y]) + maxLayerThickness;
                        int maxZ = 0;

                        for (int z = fromZ; z <= toZ; z++)
                        {
                            if (voxels.Contains(new Vector3Int(x, y, z)) && voxels[x, y, z].Layer == 0)
                            {
                                maxZ = z;
                            }
                        }

                        if (height[x, y].maxZ == 0)
                        {
                            checkHeight[x, y] = maxZ;
                        }
                        else
                        {
                            checkHeight[x, y] = height[x, y].maxZ;
                        }
                    }
                }
            })).ToArray();
            Task.WaitAll(tasks);

            //activeEdge.AsParallel().SelectMany(CheckAttached).Distinct().GroupBy(x => new Vector3Int(x.X, x.Y)).ForAll(x =>
            //{
            //    if (height[x.Key.X, x.Key.Y].maxZ == 0)
            //    {
            //        checkHeight[x.Key.X, x.Key.Y] = x.Max(x => x.Z);
            //    }
            //    else
            //    {
            //        checkHeight[x.Key.X, x.Key.Y] = height[x.Key.X, x.Key.Y].maxZ;
            //    }
            //});
        }
        private void GetMaxHeight(int[,] checkHeight)
        {
            //LOOP Y +-
            var tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
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

            //LOOP X +-

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
        }
        private void GetNextLayer()
        {
            var nextLayerVoxels = activeEdge.AsParallel().SelectMany(x => GetAttached(x)).Distinct().ToList();
            nextLayerVoxels.AsParallel().ForAll(x =>
            {
                var voxel = voxels[x];
                voxel.Layer = curLayer;
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
        }

        //################## FIRST LAYER ##################
        private void GetFirstLayer()
        {
            var firstLayer = Math.Min(maxLayerThickness, voxels.Size.Z);
            object activeEdgeLock = new object();

            var tasks = Enumerable.Range(0, voxels.Size.X).AsParallel().Select(x => Task.Run(() =>
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
            maxZ = firstLayer;
            minZ = 0;
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