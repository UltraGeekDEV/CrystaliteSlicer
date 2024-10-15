using Crystalite.Models;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public class MeshData
    {
        public static MeshData instance;
        public Mesh? selected;
        private OpenTK.Mathematics.Vector3 originalCol;

        public List<Mesh> meshes = new List<Mesh>();
        static MeshData()
        {
        }
        public void CastRay(Vector3 dir,Vector3 start)
        {
            if (selected != null)
            {
                selected.col = originalCol;
                selected.hasOutline = false;
                selected = null;
            }

            var hits = meshes.Select(x => x.RayCast(dir, start)).Where(x => x.Item1 >= 0).ToList();

            if (hits.Count() > 0)
            {
                selected = hits.MinBy(x => x.Item1).Item2;
                originalCol = selected.col;
                selected.hasOutline = true;
                selected.col = new OpenTK.Mathematics.Vector3(1.0f,0.0f,0.0f);
            }
        }
    }
}
