using CrystaliteSlicer.ToolpathGeneration;
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
        IVoxelCollection chunked;

        float zVoxelsPerX;
        int curLayer = 1;

        List<Vector3Int> activeEdge = new List<Vector3Int>();
        List<Vector3Int> neighbours;
        public void GetLayers(IVoxelCollection voxels)
        {
            this.voxels = voxels;
            nozzleSize = new Vector3Int((new Vector3(Settings.NozzleDiameter, Settings.NozzleDiameter, 0) / Settings.Resolution) * (1.0f - Settings.OverhangOverlap));
            maxLayerThickness = Math.Max((int)(Settings.MaxLayerHeight / Settings.Resolution.Z), 1);

            zVoxelsPerX = (MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f)) * Settings.Resolution.X / Settings.Resolution.Z);

            chunked = new ChunkedVoxelArray(voxels, nozzleSize.X);

            neighbours = LUTS.neighbours.ToList();
            for (int i = 1; i < maxLayerThickness; i++)
            {
                neighbours.Add(new Vector3Int(0, 0, i + 1));
            }

            var queue = new Queue<Vector3Int>();

            for (int x = 0; x < chunked.Size.X; x++)
            {
                for (int y = 0; y < chunked.Size.Y; y++)
                {
                    for (int z = 0; z < maxLayerThickness; z++)
                    {
                        if (chunked.Contains(new Vector3Int(x, y, z)))
                        {
                            var voxel = chunked[x, y, z];
                            voxel.Layer = curLayer;
                            chunked[x, y, z] = voxel;
                            activeEdge.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            curLayer++;

            Debug.WriteLine($"Max possible voxel count: {chunked.Size.X*chunked.Size.Y*maxLayerThickness}");

            activeEdge = FilterActiveEdge(activeEdge);
            GetNextLayer();

            while(activeEdge.Count > 0)
            {
                GetNextLayer();
            }
            voxels.LayerCount = curLayer - 1;
        }
        private void GetNextLayer()
        {
            var valid = GetPossibleVoxels(activeEdge);
            foreach (var pos in valid)
            {
                var voxel = chunked[pos];
                voxel.Layer = curLayer;
                chunked[pos] = voxel;
                activeEdge.Add(pos);
            }

            activeEdge = FilterActiveEdge(activeEdge);

            if (valid.Count == 0)
            {
                activeEdge.Clear();
            }
            Debug.WriteLine($"Finished layer {curLayer} with {activeEdge.Count} voxels in active edge");
            curLayer++;
        }
        //Get all voxels from collection that still have active,spreadable faces
        private List<Vector3Int> FilterActiveEdge(IEnumerable<Vector3Int> activeEdge)
        {
            return activeEdge.Where(x => LUTS.neighbours.Any(y =>
            {
                var pos = x + y;
                return chunked.Contains(pos) && chunked[pos].Layer == 0;
            })).ToList();
        }

        //Gets all possible next layer voxels regardless of collision possibility
        private List<Vector3Int> GetPossibleVoxels(IEnumerable<Vector3Int> curLayer)
        {
            return curLayer.SelectMany(x=>neighbours.Select(y=>x+y)).Where(x=>chunked.Contains(x) && chunked[x].Layer == 0).Distinct().ToList();
        }
        //Filter out blocking values
        private List<Vector3Int> FilterBlocking(IEnumerable<Vector3Int> curLayer)
        {
            float[,] maxHeight = new float[chunked.Size.X, chunked.Size.Y];
            var activeVoxels = curLayer.Select(x => new Vector3Int(x.X, x.Y, 0)).Distinct().ToHashSet();

            foreach (var voxel in curLayer)
            {
                maxHeight[voxel.X, voxel.Y] = MathF.Max(maxHeight[voxel.X, voxel.Y], voxel.Z);
            }

            float curValue = float.MaxValue;

            for (int x = 0; x < chunked.Size.X; x++)
            {
                for (int y = 0; y < chunked.Size.Y; y++)
                {
                    if (activeVoxels.Contains(new Vector3Int(x,y,0)))
                    {
                        curValue = MathF.Min(curValue + zVoxelsPerX, maxHeight[x, y]);
                    }
                    else
                    {
                        curValue += zVoxelsPerX;
                    }
                    maxHeight[x, y] = curValue;
                }
            }

            for (int x = chunked.Size.X-1; x >= 0; x--)
            {
                for (int y = chunked.Size.Y-1; y >= 0; y--)
                {
                    if (activeVoxels.Contains(new Vector3Int(x, y, 0)))
                    {
                        curValue = MathF.Min(curValue + zVoxelsPerX, maxHeight[x, y]);
                    }
                    else
                    {
                        curValue += zVoxelsPerX;
                    }
                    maxHeight[x, y] = curValue;
                }
            }

            return curLayer.Where(x => maxHeight[x.X, x.Y] >= x.Z).ToList();
        }
    }
}