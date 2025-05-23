using CrystaliteSlicer.InfillGeneration;
using Models;
using Models.GcodeInfo;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.ToolpathGeneration
{
    public class NearestNeighborToolpath : IGenerateToolpath
    {
        private static class LUTS
        {
            public static List<Vector3Int> neighbours = new List<Vector3Int>();
            public static List<Vector3Int> farNeighbours = new List<Vector3Int>();

            public static List<Vector3Int> neighboursFlipped = new List<Vector3Int>();
            public static List<Vector3Int> farNeighboursFlipped = new List<Vector3Int>();

            static LUTS()
            {
                for (int i = -2; i < 3; i++)
                {
                    for (int j = -2; j < 3; j++)
                    {
                        if (i != 0 || j != 0)
                        {
                            var vec = new Vector3Int(i, j);
                            if (vec.SQRMagnitude() <= 2)
                            {
                                neighbours.Add(vec);
                            }
                            else
                            {
                                farNeighbours.Add(vec);
                            }
                        }
                    }
                }


                neighboursFlipped = neighbours.ToList();
                neighboursFlipped.Reverse();


            }
        }

        int nozzleVoxelSize;
        int halfNozzleVoxelSize;
        IVoxelCollection voxels;
        IInfill infillPattern;
        List<Dictionary<Vector3Int, (int height, int thickness, int wallCount)>> layers;
        int topThickness = (int)(Settings.TopThickness / Settings.Resolution.Z);
        public NearestNeighborToolpath(IVoxelCollection voxels, IInfill infillPattern)
        {
            this.infillPattern = infillPattern;
            layers = new List<Dictionary<Vector3Int, (int height, int thickness, int wallCount)>>();
            nozzleVoxelSize = (int)(Settings.NozzleDiameter / Settings.Resolution.X);
            halfNozzleVoxelSize = nozzleVoxelSize / 2;
            
            this.voxels = voxels;
        }

        public IGenerateToolpath SplitLayers(IVoxelCollection voxels)
        {

            (int height, int thickness)[,] curHeight = new (int height, int thickness)[voxels.Size.X, voxels.Size.Y];
            (int height, int thickness)[,] layerData = new (int height, int thickness)[voxels.Size.X, voxels.Size.Y];

            for (int layer = 1; layer < voxels.LayerCount; layer++)
            {
                for (int x = 0; x < voxels.Size.X; x++)
                {
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        curHeight[x, y].thickness = 0;
                        while (curHeight[x, y].height < voxels.Size.Z 
                            && ((curHeight[x,y].thickness == 0 && voxels[x, y, curHeight[x, y].height].Layer < layer) || (voxels[x, y, curHeight[x, y].height].Layer == layer)))
                        {
                            if (voxels[x,y,curHeight[x, y].height].Layer == layer)
                            {
                                curHeight[x, y] = (curHeight[x, y].height + 1, curHeight[x, y].thickness + 1);
                            }
                            else
                            {
                                curHeight[x, y] = (curHeight[x, y].height + 1, curHeight[x, y].thickness);
                            }
                        }
                        curHeight[x, y] = (curHeight[x, y].height - 1, curHeight[x, y].thickness);
                    }
                }
                for (int x = 0; x < voxels.Size.X; x++)
                {
                    for (int y = 0; y < voxels.Size.Y; y++)
                    {
                        if (curHeight[x, y].thickness == 0)
                        {
                            layerData[x, y] = (0, 0);
                        }
                        else
                        {
                            layerData[x, y] = curHeight[x, y];
                        }
                    }
                }
                AddLayer(layerData);
            }
            return this;
        }
        public void AddLayer((int height, int thickness)[,] layer)
        {
            int width = layer.GetLength(0);
            int depth = layer.GetLength(1);

            List<Vector3Int>[] points = new List<Vector3Int>[depth];

            var tasks = Enumerable.Range(0, depth).Select(y => Task.Run(() =>
            {
                points[y] = new List<Vector3Int>();
                for (int x = 0; x < width; x++)
                {
                    if (layer[x,y].height != 0) {
                        bool isLine = IsLine(x, y, layer[x, y].height);
                        bool isShell = IsShell(x, y, layer[x, y].height);
                        bool isFill = infillPattern.IsFill(voxels[x, y, layer[x, y].height].XYDepth, voxels[x, y, layer[x, y].height].ZDepth, x, y, layer[x, y].height);
                        if (isLine || (!isShell && isFill))
                        {
                            points[y].Add(new Vector3Int(x, y, layer[x, y].height));
                        }
                    }
                }
               
            })).ToArray();
            Task.WaitAll(tasks);
            var curLayer = points.AsParallel().SelectMany(x => x).ToDictionary(x => new Vector3Int(x.X, x.Y), x => (x.Z, layer[x.X, x.Y].thickness, Math.Min(voxels[x.X,x.Y,layer[x.X, x.Y].height].XYDepth / nozzleVoxelSize,Settings.WallCount)));
            layers.Add(curLayer);
        }
        private bool IsLine(int x, int y, int z)
        {
            var value = voxels[x, y, z].XYDepth;
            return Math.Abs((value % nozzleVoxelSize) - halfNozzleVoxelSize) == 0 && value >= 0 && IsShell(x,y,z);
        }
        private bool IsShell(int x, int y, int z)
        {
            var value = voxels[x, y, z].XYDepth;
            return value / nozzleVoxelSize < Settings.WallCount || voxels[x, y, z].ZDepth < topThickness;
        }
        public IEnumerable<Line> GetPath()
        {
            var tasks = layers.Select(x => x.GroupBy(y => y.Value.wallCount).OrderBy(y=>y.Key).
            Select(y => Task.Run(()=> GetLayerPath(y.ToDictionary(z => z.Key, z => (z.Value.height, z.Value.thickness)), y.Key))));
            Task.WaitAll(tasks.SelectMany(x=>x).ToArray());

            var layerPaths = tasks.SelectMany(x => x.Select(x => x.Result)).Where(x=>x.Count > 0).ToList();

            List<Line> combinedPath = new List<Line>();
            int count = 0;

            foreach (var layer in layerPaths)
            {
                if (count < layerPaths.Count - 1 && count > 0)
                {
                    combinedPath.Add(new NewLayerLine());
                    combinedPath.Add(new Line(layerPaths[count - 1].Last(x => !(x is InfoLine)).End, layer.First(x => !(x is InfoLine)).Start, 0, true));
                }
                foreach (var item in layer)
                {
                    combinedPath.Add(item);
                }
                count++;
            }

            return combinedPath;
        }

        private List<Line> GetLayerPath(Dictionary<Vector3Int, (int height, int thickness)> pointData, int wallCount)
        {
            var direction = new Random().Next(0, 2);

            if (pointData.Count < 2)
            {
                return new List<Line>();
            }

            var retPath = new List<Line>(pointData.Count);

            if (wallCount == 0)
            {
                retPath.Add(new WallLine());
            }
            else if(wallCount < Settings.WallCount)
            {
                retPath.Add(new InnerWallLine());
            }
            else
            {
                retPath.Add(new InfillLine());
            }

            var cur = pointData.First();
            pointData.Remove(cur.Key);
            while (pointData.Count > 0)
            {
                Vector3Int next;

                if (direction == 0)
                {
                    next = LUTS.neighbours.Select(x => x + cur.Key).FirstOrDefault(pointData.ContainsKey,new Vector3Int(-1,-1,-1));
                }
                else
                {
                    next = LUTS.neighboursFlipped.Select(x => x + cur.Key).FirstOrDefault(pointData.ContainsKey, new Vector3Int(-1, -1, -1));
                }
                
                KeyValuePair<Vector3Int, (int height, int thickness)> pick;
                if (!next.Equals(new Vector3Int(-1, -1, -1)))
                {
                    pick = new KeyValuePair<Vector3Int, (int height, int thickness)>(next, pointData[next]);
                }
                else
                {
                    pick = pointData.MinBy(x => (x.Key - cur.Key).SQRMagnitude());
                }


                retPath.Add(new Line(new Vector3(cur.Key.X, cur.Key.Y, cur.Value.height+1f) * Settings.Resolution-new Vector3(0,0,0.2f)+voxels.LowerLeft, new Vector3(pick.Key.X, pick.Key.Y, pick.Value.height + 1f) * Settings.Resolution - new Vector3(0, 0, 0.2f) + voxels.LowerLeft
                    , (cur.Value.thickness+pick.Value.thickness)*0.5f,Math.Abs(pick.Key.X-cur.Key.X) > 1 || Math.Abs(pick.Key.Y - cur.Key.Y) > 1));
                cur = pick;
                pointData.Remove(cur.Key);
            }
            return retPath;
        }
    }
}
