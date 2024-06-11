using CrystaliteSlicer.MeshImport;
using Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CrystaliteSlicer.Voxelize
{
    public class Voxelizer : IVoxelize
    {
        const int fillVoxelValue = int.MaxValue;
        const int shellVoxelValue = 1;
        public IVoxelCollection Voxelize(IEnumerable<Triangle> triangles)
        {
            var mesh = triangles.ToList();
            mesh.AsParallel().ForAll(x =>
            {
                x.A = Utils.GetID(x.a);
                x.B = Utils.GetID(x.b);
                x.C = Utils.GetID(x.c);
            });
            var lowerLeft = mesh.First().A;
            mesh.ForEach(x =>
            {
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.A, (a, b) => a > b);
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.B, (a, b) => a > b);
                lowerLeft = Utils.CompareAndSwap(lowerLeft, x.C, (a, b) => a > b);
            });
            var upperRight = mesh.First().A;
            mesh.ForEach(x =>
            {
                upperRight = Utils.CompareAndSwap(upperRight, x.A, (a, b) => a < b);
                upperRight = Utils.CompareAndSwap(upperRight, x.B, (a, b) => a < b);
                upperRight = Utils.CompareAndSwap(upperRight, x.C, (a, b) => a < b);
            });

            upperRight += Vector3Int.One;

            IVoxelCollection voxels = new FlatVoxelArray(upperRight-lowerLeft);

            mesh.AsParallel().Where(x => x.GetNormal().Z < -0.01f).Select(tri => (tri.GetVoxelsBresenham(), tri)).ForAll(x =>
            {
                var normDir = x.tri.GetNormal().Z;
                foreach (var item in x.Item1)
                {
                    voxels[item - lowerLeft] = new VoxelData() { depth = shellVoxelValue, normZ = normDir };
                }
            });

            mesh.AsParallel().Where(x=> Math.Abs(x.GetNormal().Z) <= 0.01f).Select(tri => (tri.GetVoxelsBresenham(),tri)).ForAll(x =>
            {
                var normDir = x.tri.GetNormal().Z;
                foreach (var item in x.Item1)
                {
                    voxels[item - lowerLeft] = new VoxelData() { depth = shellVoxelValue, normZ = normDir };
                }
            });
            
            mesh.AsParallel().Where(x => x.GetNormal().Z > 0.01f).Select(tri => (tri.GetVoxelsBresenham(), tri)).ForAll(x =>
            {
                var normDir = x.tri.GetNormal().Z;
                foreach (var item in x.Item1)
                {
                    voxels[item - lowerLeft] = new VoxelData() { depth = shellVoxelValue, normZ = normDir };
                }
            });

            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => FillVoxelMesh(voxels, x)));
            Task.WaitAll(tasks.ToArray());
            tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => RemoveExtra(voxels, x)));
            Task.WaitAll(tasks.ToArray());
            tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => RemoveExtra(voxels, x)));
            Task.WaitAll(tasks.ToArray());
            return voxels;
        }
        private bool HasOpenFace(Vector3Int check,IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Any(x => !voxels.Contains(x + check));
        }
        private IEnumerable<Vector3Int> GetNeighbours(Vector3Int check, IVoxelCollection voxels)
        {
            return LUTS.faceOffsets.Select(x => x+check).Where(x=>voxels.Contains(x) && voxels[x].depth == fillVoxelValue && LUTS.faceOffsets.Count(x => !voxels.Contains(x + check)) > 1);
        }
        private void FillVoxelMesh(IVoxelCollection voxels,int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                bool draw = false;
                bool remainsInShell = false;
                bool enterShell = false;
                for (int z = 0; z < voxels.Size.Z; z++)
                {
                    if (voxels[x, y, z].depth == shellVoxelValue)
                    {
                        if (Math.Abs(voxels[x,y,z].normZ) >= 0.01f)
                        {
                            draw = voxels[x, y, z].normZ < 0;
                        }
                    }
                    else
                    {
                        if (draw)
                        {
                            voxels[x, y, z] = new VoxelData() { depth = fillVoxelValue };
                        }
                        else{
                            voxels[x, y, z] = new VoxelData() { depth = -1 };
                        }
                    }
                }
                for (int z = voxels.Size.Z-1; z >= 0; z--)
                {
                    if (voxels[x, y, z].depth == shellVoxelValue)
                    {
                        if (Math.Abs(voxels[x, y, z].normZ) >= 0.01f)
                        {
                            draw = voxels[x, y, z].normZ > 0;
                        }
                    }
                    else
                    {
                        if (draw)
                        {
                            voxels[x, y, z] = new VoxelData() { depth = fillVoxelValue };
                        }
                        else
                        {
                            //voxels[x, y, z] = new VoxelData() { depth = -1 };
                        }
                    }
                }
            }
        }

        private void RemoveExtra(IVoxelCollection voxels, int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                for (int z = voxels.Size.Z - 1; z >= 0; z--)
                {
                    if (voxels[x, y, z].depth != shellVoxelValue)
                    {
                        var check = new Vector3Int(x, y, z);
                        var depths = LUTS.flatNeighbours.Select(x=>x+check).Where(voxels.ValidID).Select(x => voxels[x].depth).ToList();
                        var fillCount = depths.Count(x=>x==fillVoxelValue);
                        var airCount = depths.Count(x=>x==-1);

                        if (fillCount > airCount)
                        {
                            voxels[x, y, z] = voxels[x, y, z] = new VoxelData() { depth = fillVoxelValue };
                        }
                        else
                        {
                            voxels[x, y, z] = new VoxelData() { depth = -1 };
                        }
                    }
                }
            }
        }
    }
}
