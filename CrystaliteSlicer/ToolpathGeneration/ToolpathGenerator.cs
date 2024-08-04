using Models;
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
        List<List<Vector3Int>> layers;
        List<Dictionary<Vector3Int, int>> thickness;
        float zPerX;
        public ToolpathGenerator()
        {
            layers = new List<List<Vector3Int>>();
            thickness = new List<Dictionary<Vector3Int, int>>();

            nozzleVoxelSize = (int)(Settings.NozzleDiameter / Settings.Resolution.X);
            halfNozzleVoxelSize = nozzleVoxelSize / 2;
            zPerX = MathF.Tan(Settings.MaxSlope * (MathF.PI / 180.0f));
        }
        public void AddLayer((int height, int thickness)[,] layer)
        {
            int width = layer.GetLength(0);
            int depth = layer.GetLength(1);

            int[,] df = new int[width, depth];
            List<Vector3Int>[] points = new List<Vector3Int>[depth];

            var tasks = Enumerable.Range(0, width).AsParallel().Select(x => Task.Run(() =>
            {
                int curDist = int.MinValue;
                for (int y = 0; y < depth; y++)
                {
                    if (layer[x,y].height != -1)
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
                    if (layer[x, y].height != -1)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(curDist++, df[x,y]);
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
            tasks = Enumerable.Range(0, depth).AsParallel().Select(y => Task.Run(() =>
            {
                points[y] = new List<Vector3Int>();
                int curDist = int.MinValue;
                for (int x = 0; x < depth; x++)
                {
                    if (layer[x, y].height != -1)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(curDist++, df[x, y]);
                        }
                    }
                    else
                    {
                        curDist = int.MinValue;
                    }

                    df[x, y] = curDist;
                }
                curDist = int.MinValue;
                for (int x = depth - 1; x >= 0; x--)
                {
                    if (layer[x, y].height != -1)
                    {
                        if (curDist < 0)
                        {
                            curDist = 0;
                        }
                        else
                        {
                            curDist = Math.Min(curDist++, df[x, y]);
                        }
                    }
                    else
                    {
                        curDist = int.MinValue;
                    }

                    df[x, y] = curDist;

                    if (IsLine(curDist))
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
        private bool IsLine(int value)
        {
            return Math.Abs((value % nozzleVoxelSize)-halfNozzleVoxelSize) == 0 && value/nozzleVoxelSize < Settings.WallCount && value >= 0;
        }
        public IEnumerable<Line> GetPath()
        {
            var tasks = layers.Select((x, id) => Task<List<Line>>.Run(() => GetLayerPath(x, thickness[id]))).ToArray();
            Task.WaitAll(tasks);

            var finalPath = new List<Line>();

            var layerPaths = tasks.Select(x => x.Result).Where(x=>x.Count > 0).ToList();

            Vector3 prevPoint = -Vector3.One;
            foreach (var layer in layerPaths)
            {
                int segmentCount = 0;
                Line cur = layer.First();
                if (prevPoint.Z > 0)
                {
                    Vector3 path = cur.Start - prevPoint;
                    path.Z = 0;
                    float climbFraction = cur.Start.Z / (cur.Start.Z + prevPoint.Z);
                    var midpoint = prevPoint + path * climbFraction + new Vector3(0, 0, path.Length() * climbFraction * zPerX * 1.25f);

                    finalPath.Add(new Line(prevPoint, midpoint, 0, true));
                    finalPath.Add(new Line(midpoint, cur.Start, 0, true));
                }

                foreach (var segment in layer.Skip(1))
                {
                    var curDir = Vector3.Normalize(cur.End-cur.Start);
                    var nextDir = Vector3.Normalize(segment.End - segment.Start);

                    if (Vector3.Dot(curDir, nextDir) >= Settings.SmoothingAngle && segmentCount < Settings.SmoothingCount && cur.Travel.Equals(segment.Travel))
                    {
                        cur = new Line(cur.Start,segment.End,(cur.Flow+segment.Flow)/2,cur.Travel);
                        segmentCount++;
                    }
                    else if(cur.Travel)
                    {
                        Vector3 path = cur.End - cur.Start;
                        path.Z = 0;
                        float climbFraction = cur.End.Z / (cur.End.Z + cur.Start.Z);
                        var midpoint = cur.Start + path * climbFraction + new Vector3(0, 0, path.Length() * climbFraction * zPerX * 1.25f);

                        finalPath.Add(new Line(cur.Start, midpoint,0,true));
                        finalPath.Add(new Line(midpoint, cur.End ,0,true));
                    }
                    else
                    {
                        finalPath.Add(cur);
                    }
                    prevPoint = cur.End;
                }

                if (cur.Travel)
                {
                    Vector3 path = cur.End - cur.Start;
                    path.Z = 0;
                    float climbFraction = cur.End.Z / (cur.End.Z + cur.Start.Z);
                    var midpoint = cur.Start + path * climbFraction + new Vector3(0, 0, path.Length() * climbFraction * zPerX * 1.25f);

                    finalPath.Add(new Line(cur.Start, midpoint, 0, true));
                    finalPath.Add(new Line(midpoint, cur.End, 0, true));

                    segmentCount = 0;
                }
                else
                {
                    finalPath.Add(cur);

                    segmentCount = 0;
                }
            }

            return finalPath;
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
                        pheromones[trail] += overallPathEval;
                    }
                }
            }

            var toTraverse = points.ToList();

            Vector3Int cur = toTraverse.First();
            toTraverse.Remove(cur);
            var retPath = new List<Line>();

            while (toTraverse.Count > 0)
            {
                var next = toTraverse.OrderByDescending(x => pheromones[(cur, x)]).First();
                retPath.Add(new Line(cur * Settings.Resolution, next * Settings.Resolution, (thickness[cur] + thickness[next])*0.5f,(next-cur).SQRMagnitude() > 2.5));
                toTraverse.Remove(next);
                cur = next;
            }

            return retPath;
        }
    }
}
