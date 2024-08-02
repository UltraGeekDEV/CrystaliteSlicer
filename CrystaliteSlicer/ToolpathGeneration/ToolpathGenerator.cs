using Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public ToolpathGenerator()
        {
            layers = new List<List<Vector3Int>>();
            thickness = new List<Dictionary<Vector3Int, int>>();

            nozzleVoxelSize = (int)(Settings.NozzleDiameter / Settings.Resolution.X);
            halfNozzleVoxelSize = nozzleVoxelSize / 2;
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

                    df[y,x] = curDist;
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

                    df[y, x] = curDist;
                }
            })).ToArray();
            Task.WaitAll(tasks);
            tasks = Enumerable.Range(0, depth).AsParallel().Select(y => Task.Run(() =>
            {
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

                    df[y, x] = curDist;
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

                    df[y, x] = curDist;

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
            throw new NotImplementedException();
        }
    }
}
