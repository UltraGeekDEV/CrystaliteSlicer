using CrystaliteSlicer.InfillGeneration;
using Models;
using Models.GcodeInfo;
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
            public static List<Vector3Int> neighbours = new List<Vector3Int>()
            {
                new Vector3Int(1,0),
                new Vector3Int(1,1),
                new Vector3Int(0,1),
                new Vector3Int(-1,1),
                new Vector3Int(-1,0),
                new Vector3Int(-1,-1),
                new Vector3Int(0,-1),
                new Vector3Int(1,-1)
            };
        }

        int nozzleVoxelSize;
        int halfNozzleVoxelSize;
        IVoxelCollection voxels;
        IInfill infillPattern;
        List<Dictionary<Vector3Int, (int height, int thickness, int wallCount)>> layers;
        float zPerX;
        public NearestNeighborToolpath(IVoxelCollection voxels, IInfill infillPattern)
        {
            this.infillPattern = infillPattern;
            layers = new List<Dictionary<Vector3Int, (int height, int thickness, int wallCount)>>();

            nozzleVoxelSize = (int)(Settings.NozzleDiameter / Settings.Resolution.X);
            halfNozzleVoxelSize = nozzleVoxelSize / 2;
            zPerX = MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f));
            this.voxels = voxels;
        }
        public void AddLayer((int height, int thickness)[,] layer)
        {
            int width = layer.GetLength(0);
            int depth = layer.GetLength(1);

            int[,] df = new int[width, depth];
            List<Vector3Int>[] points = new List<Vector3Int>[depth];

            var tasks = Enumerable.Range(0, width).Select(x => Task.Run(() =>
            {
                int curDist = int.MinValue;
                for (int y = 0; y < depth; y++)
                {
                    if (layer[x, y].height != 0)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist++;
                        }
                    }
                    else
                    {
                        curDist = int.MinValue;
                    }

                    df[x, y] = curDist;
                }
                for (int y = depth - 1; y >= 0; y--)
                {
                    if (layer[x, y].height != 0)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(++curDist, df[x, y]);
                        }
                    }
                    else
                    {
                        curDist = int.MinValue;
                    }

                    df[x, y] = curDist;
                }
            })).ToArray();
            Task.WaitAll(tasks);
            tasks = Enumerable.Range(0, depth).Select(y => Task.Run(() =>
            {
                points[y] = new List<Vector3Int>();
                int curDist = int.MinValue;
                for (int x = 0; x < width; x++)
                {
                    if (layer[x, y].height != 0)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(++curDist, df[x, y]);
                        }
                    }
                    else
                    {
                        curDist = int.MinValue;
                    }

                    df[x, y] = curDist;
                }
                curDist = int.MinValue;
                for (int x = width - 1; x >= 0; x--)
                {
                    if (layer[x, y].height != 0)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(++curDist, df[x, y]);
                        }
                    }
                    else
                    {
                        curDist = int.MinValue;
                    }

                    df[x, y] = curDist;

                    bool isLine = IsLine(curDist, x, y, layer[x, y].height);
                    bool isShell = IsShell(curDist, x, y, layer[x, y].height);
                    bool isFill = infillPattern.IsFill(curDist, voxels[x, y, layer[x, y].height].depth, x, y, layer[x, y].height);
                    if (isLine || (!isShell && isFill))
                    {
                        points[y].Add(new Vector3Int(x, y, layer[x, y].height));
                    }
                }
            })).ToArray();
            Task.WaitAll(tasks);
            var curLayer = points.AsParallel().SelectMany(x => x).ToDictionary(x => new Vector3Int(x.X, x.Y), x => (x.Z, layer[x.X, x.Y].thickness, Math.Min(df[x.X, x.Y] / nozzleVoxelSize,Settings.WallCount)));
            layers.Add(curLayer);
        }
        private bool IsLine(int value, int x, int y, int z)
        {
            return Math.Abs((value % nozzleVoxelSize) - halfNozzleVoxelSize) == 0 && value >= 0 && IsShell(value,x,y,z);
        }
        private bool IsShell(int value, int x, int y, int z)
        {
            return value / nozzleVoxelSize < Settings.WallCount || voxels[x, y, z].depth < Settings.TopThickness;
        }
        public IEnumerable<Line> GetPath()
        {
            //var tasks = layers.Select(y => Task<List<Line>>.Run(() => GetLayerPath(y))).ToArray();
            var tasks = layers.Select(x=>x.GroupBy(y=>y.Value.wallCount)).Select(x=>x.Select(y => (Task<List<Line>>.Run(() => GetLayerPath(y.ToDictionary(z=>z.Key,z=>(z.Value.height,z.Value.thickness)),y.Key)),y.Key)));
            Task.WaitAll(tasks.SelectMany(x=>x.Select(y=>y.Item1)).ToArray());

            var combinedPath = new List<Line>();

            var layerPaths = tasks.Select(x => x.OrderBy(y => y.Key).SelectMany(y=>y.Item1.Result).ToList()).Where(y => y.Count > 0).ToList();

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
            if (pointData.Count < 2)
            {
                return new List<Line>();
            }

            var retPath = new List<Line>();

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

            while(pointData.Count > 0)
            {
                var candidates = LUTS.neighbours.Select(x => x + cur.Key).Where(pointData.ContainsKey);
                KeyValuePair<Vector3Int, (int height, int thickness)> pick;
                if (candidates.Any())
                {
                    var pickPos = candidates.First();
                    pick = new KeyValuePair<Vector3Int, (int height, int thickness)>(pickPos, pointData[pickPos]);
                }
                else
                {
                    pick = pointData.MinBy(x => (x.Key - cur.Key).SQRMagnitude());
                }

                retPath.Add(new Line(new Vector3Int(cur.Key.X, cur.Key.Y, cur.Value.height) * Settings.Resolution+Settings.Offset, new Vector3Int(pick.Key.X, pick.Key.Y, pick.Value.height) * Settings.Resolution + Settings.Offset, (cur.Value.thickness+pick.Value.thickness)*0.5f,Math.Abs(pick.Key.X-cur.Key.X) > 1 || Math.Abs(pick.Key.Y - cur.Key.Y) > 1));
                cur = pick;
                pointData.Remove(cur.Key);
            }

            return retPath;
        }
    }
}
