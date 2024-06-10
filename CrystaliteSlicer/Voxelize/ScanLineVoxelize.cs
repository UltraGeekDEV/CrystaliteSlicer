using Assimp;
using Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Voxelize
{
    public class ScanLineVoxelize : IVoxelize
    {
        public IVoxelCollection Voxelize(IEnumerable<Triangle> triangles)
        {
            var mesh = triangles.ToList();
            var lowerLeft = mesh.First().a;
            mesh.ForEach(x =>
            {
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.a, (a, b) => a > b);
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.b, (a, b) => a > b);
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.c, (a, b) => a > b);
            });
            var upperRight = mesh.First().a;
            mesh.ForEach(x =>
            {
                upperRight = Utils.CompareAndSwap(upperRight, x.a, (a, b) => a < b);
                upperRight = Utils.CompareAndSwap(upperRight, x.b, (a, b) => a < b);
                upperRight = Utils.CompareAndSwap(upperRight, x.c, (a, b) => a < b);
            });

            mesh = mesh.Select(x => new Triangle { a = x.a-lowerLeft,b = x.b-lowerLeft,c = x.c-lowerLeft}).ToList();

            Vector3Int domainSize = Utils.GetID(upperRight) - Utils.GetID(lowerLeft) + Vector3Int.One;

            IVoxelCollection voxels = new FlatVoxelArray(domainSize);

            var tasks = Enumerable.Range(0, domainSize.X).SelectMany(x=> Enumerable.Range(0, domainSize.Y).Select(y =>
            {
                return Task.Run(()=>ScanLine(x,y,voxels,mesh));
            })).ToArray();

            Task.WaitAll(tasks);

            return voxels;
        }

        private void ScanLine(int x, int y,IVoxelCollection voxels,IEnumerable<Triangle> tris)
        {
            var aabb = new AABB() {lowerLeft = Utils.GetPos(new Vector3Int(x,y,-1))-Settings.Resolution,upperRight = Utils.GetPos(new Vector3Int(x, y, voxels.Size.Z+1)) + Settings.Resolution };
            var applicableTris = tris.Where(aabb.Intersects);

            var activeTris = applicableTris.ToList();

            bool draw = false;

            if (activeTris.Count > 0)
            {
                var minZ = ((int)(activeTris.Min(x => Math.Min(x.c.Z, (Math.Min(x.a.Z, x.b.Z)))) / Settings.Resolution.Z)) - 1;
                var maxZ = ((int)(activeTris.Max(x => Math.Max(x.c.Z, (Math.Max(x.a.Z, x.b.Z)))) / Settings.Resolution.Z)) + 1;
                Triangle? curTri = null;
                for (int z = minZ; z < maxZ; z++)
                {
                    var voxel = Utils.GetAABB(new Vector3Int(x, y, z));
                    var tri = activeTris.FirstOrDefault(voxel.Intersects);
                    if (tri != null)
                    {
                        draw = tri.GetNormal().Z > -0.0001f;
                        curTri = tri;
                    }
                    else if(curTri != null)
                    {
                        activeTris.Remove(curTri);
                        curTri = null;
                    }
                    if (draw)
                    {
                        voxels[new Vector3Int(x, y, z)] = new VoxelData() { depth = 1 };
                    }
                }
            }
        }
    }
}
