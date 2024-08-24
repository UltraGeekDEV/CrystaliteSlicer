using CrystaliteSlicer.InfillGeneration;
using Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.ToolpathGeneration
{
    public class ToolpathGenerator : IGenerateToolpath
    {
        int nozzleVoxelSize;
        int halfNozzleVoxelSize;
        IVoxelCollection voxels;
        IInfill infillPattern;
        List<List<Vector3Int>> layers;
        List<Dictionary<Vector3Int, int>> thickness;
        float zPerX;
        public ToolpathGenerator(IVoxelCollection voxels)
        {
            layers = new List<List<Vector3Int>>();
            thickness = new List<Dictionary<Vector3Int, int>>();

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
                    if (layer[x,y].height != 0)
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

                    df[x,y] = curDist;
                }
                for (int y = depth-1; y >= 0; y--)
                {
                    if (layer[x, y].height != 0)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(++curDist, df[x,y]);
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
                        points[y].Add(new Vector3Int(x, y, layer[x,y].height));
                    }
                }
            })).ToArray();
            Task.WaitAll(tasks);
            var curLayer = points.AsParallel().SelectMany(x => x).ToList();
            layers.Add(curLayer);
            thickness.Add(curLayer.AsParallel().ToDictionary(x => x, x => layer[x.X,x.Y].thickness)); 
        }
        private bool IsLine(int value,int x,int y, int z)
        {
            return  Math.Abs((value % nozzleVoxelSize)-halfNozzleVoxelSize) == 0  && value >= 0 && IsShell(value,x,y,z);
        }
        private bool IsShell(int value, int x, int y, int z)
        {
            return value / nozzleVoxelSize < Settings.WallCount || voxels[x, y, z].depth < Settings.TopThickness;
        }
        public IEnumerable<Line> GetPath()
        {
            var tasks = layers.Select((x, id) => Task<List<Line>>.Run(() => GetLayerPath(x, thickness[id]))).ToArray();
            Task.WaitAll(tasks);

            var combinedPath = new List<Line>();

            var layerPaths = tasks.Select(x => x.Result).Where(x => x.Count > 0).ToList();

            int count = 0;

            foreach (var layer in layerPaths)
            {
                if (count < layerPaths.Count - 1 && count > 0)
                {
                    combinedPath.Add(new Line(layerPaths[count - 1].Last().End, layer.First().Start, -1, true));
                }
                foreach (var item in layer)
                {
                    combinedPath.Add(item);
                }
                count++;
            }

            return combinedPath;
        }

        private List<Line> GetLayerPath(List<Vector3Int> points, Dictionary<Vector3Int, int> thickness)
        {
            if (points.Count < 2)
            {
                return new List<Line>();
            }
            var pheromones = new Dictionary<(Vector3Int, Vector3Int), double>();

            for (int i = 0; i < Settings.StepCount; i++)
            {
                foreach (var item in pheromones.Keys.ToList())
                {
                    pheromones[item] = pheromones[item] * Settings.PheromoneDecayFactor;
                }

                var ants = Enumerable.Range(0, Settings.AntCount).Select(x => new Ant(pheromones)).ToList();
                var tasks = ants.Select(x => Task.Run(() => x.Traverse(points.ToHashSet()))).ToArray();
                Task.WaitAll(tasks);

                foreach (var ant in ants)
                {
                    double dist = 0, deltaDir = 0;
                    Vector3 prevDir = Vector3.Zero;
                    foreach (var trail in ant.Path)
                    {
                        dist += (trail.Item1 - trail.Item2).Magnitude();
                        deltaDir += 1 + Vector3.Dot(prevDir, (trail.Item2 - trail.Item1)*1.0f);
                    }

                    double overallPathEval = 1 / dist;

                    foreach (var trail in ant.Path)
                    {
                        if (pheromones.ContainsKey(trail))
                        {
                            pheromones[trail] += overallPathEval;
                        }
                        else
                        {
                            pheromones[trail] = overallPathEval;
                        }
                        var reverseTrail = (trail.Item2, trail.Item1);
                        if (pheromones.ContainsKey(reverseTrail))
                        {
                            pheromones[reverseTrail] += overallPathEval;
                        }
                        else
                        {
                            pheromones[reverseTrail] = overallPathEval;
                        }
                    }
                }
            }

            var toTraverse = points.ToList();

            Vector3Int cur = toTraverse.First();
            toTraverse.Remove(cur);
            var retPath = new List<Line>();

            while (toTraverse.Count > 0)
            {
                Vector3Int next = Vector3Int.One*-1;
                double nextPoint = double.MinValue;
                foreach (var item in toTraverse)
                {
                    if (pheromones.ContainsKey((cur,item)) && pheromones[(cur, item)] > nextPoint)
                    {
                        nextPoint = pheromones[(cur, item)];
                        next = item;
                    }
                    else
                    {
                        var dist = -(next - cur).SQRMagnitude();
                        if (dist > nextPoint)
                        {
                            nextPoint = dist;
                            next = item;
                        }
                    }
                }
                var dir = next - cur;
                dir = new Vector3Int(Math.Abs(dir.X), Math.Abs(dir.Y), Math.Abs(dir.Z));
                retPath.Add(new Line(cur * Settings.Resolution, next * Settings.Resolution, (thickness[cur] + thickness[next])*0.5f,!(dir.X <= 2 && dir.Y <= 2 && dir.Z <= 2)));
                toTraverse.Remove(next);
                cur = next;
            }

            return retPath;
        }
    }
}
