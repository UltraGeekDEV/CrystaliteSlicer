using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.ToolpathGeneration
{
    internal class Ant
    {
        private Random randomizer = new Random();
        private Dictionary<(Vector3Int,Vector3Int),float> pheromones;

        private List<(Vector3Int, Vector3Int)> path = new List<(Vector3Int, Vector3Int)>();

        public List<(Vector3Int, Vector3Int)> Path { get => path; set => path = value; }

        public Ant(Dictionary<(Vector3Int, Vector3Int), float> pheromones)
        {
            Path = new List<(Vector3Int, Vector3Int)>();
            this.pheromones = pheromones;
        }

        public void Traverse(HashSet<Vector3Int> points)
        {
            var curPoint = GetPoint(points);
            while(points.Count > 0)
            {
                var nextPoint = GetPoint(points, curPoint);
                path.Add((curPoint, nextPoint));
                curPoint = nextPoint;
            }
        }

        private Vector3Int GetPoint(HashSet<Vector3Int> points, Vector3Int cur)
        {
            var point = points.First();
            var maxValue = Eval(cur, point);

            foreach (var item in points)
            {
                var eval = Eval(cur, item);
                if (eval > maxValue)
                {
                    maxValue = eval;
                    point = item;
                }
            }

            points.Remove(point);

            return point;
        }
        private Vector3Int GetPoint(HashSet<Vector3Int> points)
        {
            var point = points.First();
            points.Remove(point);
            return point;
        }

        private double Eval(Vector3Int cur, Vector3Int next)
        {
            if (pheromones.ContainsKey((cur, next)))
            {
                return pheromones[(cur, next)]
                + cur.Dot(next) * Settings.DirectionChangeWeight
                - (cur - next).SQRMagnitude() * Settings.DistanceWeight
                + ((float)randomizer.NextDouble() - 0.5f) * 2 * Settings.RandomWeight;
            }
            else
            {
                return cur.Dot(next) * Settings.DirectionChangeWeight
                - (cur - next).SQRMagnitude() * Settings.DistanceWeight
                + ((float)randomizer.NextDouble() - 0.5f) * 2 * Settings.RandomWeight;
            }
        }

    }
}
