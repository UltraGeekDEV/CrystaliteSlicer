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
        const float verticalThreshold = 0f;
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

            mesh.AsParallel().Where(x=>x.GetNormal().Z < verticalThreshold).Select(tri => (tri.GetVoxelsBresenham(), tri)).ForAll(x =>
            {
                var normDir = x.tri.GetNormal();
                foreach (var item in x.Item1)
                {
                    voxels[item - lowerLeft] = new VoxelData() { depth = shellVoxelValue, norm = (byte)((0) | (normDir.Y >= verticalThreshold ? 2 : 0)) };
                }
            });
            mesh.AsParallel().Where(x => x.GetNormal().Z >= verticalThreshold).Select(tri => (tri.GetVoxelsBresenham(), tri)).ForAll(x =>
            {
                var normDir = x.tri.GetNormal();
                foreach (var item in x.Item1)
                {
                    var check = item - lowerLeft;
                    if (voxels[check].depth == shellVoxelValue && (voxels[check].norm & 1) == 0)
                    {
                        voxels[check] = default;
                    }
                    else
                    {
                        
                    }
                    voxels[check] = new VoxelData() { depth = shellVoxelValue, norm = (byte)((1) | (normDir.Y >= verticalThreshold ? 2 : 0)) };
                }
            });


            Console.WriteLine("\tFinished Shell");

            var tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => FillVoxelMesh(voxels, x)));
            Task.WaitAll(tasks.ToArray());
            //tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => FillGaps(voxels, x)));
            //Task.WaitAll(tasks.ToArray());
            //tasks = Enumerable.Range(0, voxels.Size.X).Select(x => Task.Run(() => RemoveExtra(voxels, x)));
            //Task.WaitAll(tasks.ToArray());
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
        private void FillVoxelMesh(IVoxelCollection voxels, int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                bool draw = false;
                int drawTo = 0;
                for (int z = 0; z < voxels.Size.Z; z++)
                {
                    if (voxels[x, y, z].depth == shellVoxelValue)
                    {
                        if ((voxels[x, y, z].norm & 1) == 0)
                        {
                            draw = true;
                            drawTo = z;
                        }
                    }
                    else
                    {
                        voxels[x, y, z] = new VoxelData() { depth = -1 };
                    }
                    if (draw && voxels[x, y, z].depth == shellVoxelValue && (voxels[x, y, z].norm & 1) == 1)
                    {
                        for (int i = z - 1; i > drawTo; i--)
                        {
                            voxels[x, y, i] = new VoxelData() { depth = fillVoxelValue };
                        }
                        draw = false;
                    }
                }
            }

            //for (int z = 0; z < voxels.Size.Z; z++)
            //{
            //    bool draw = false;
            //    int drawTo = 0;
            //    for (int y = 0; y < voxels.Size.Y; y++)
            //    {
            //        if (voxels[x, y, z].depth == shellVoxelValue)
            //        {
            //            if ((voxels[x, y, z].norm & 2) != 2)
            //            {
            //                draw = true;
            //                drawTo = y;
            //            }
            //        }
            //        if (draw && voxels[x, y, z].depth == shellVoxelValue && (voxels[x, y, z].norm & 2) == 2)
            //        {
            //            for (int i = y - 1; i > drawTo; i--)
            //            {
            //                voxels[x, i, z] = new VoxelData() { depth = fillVoxelValue };
            //            }
            //            draw = false;
            //        }
            //    }
            //}
        }
        private void FillGaps(IVoxelCollection voxels, int x)
        {
            for (int y = 0; y < voxels.Size.Y; y++)
            {
                for (int z =  0; z < voxels.Size.Z; z++)
                {
                    if (voxels[x, y, z].depth == -1)
                    {
                        var fillCount = 0;
                        var airCount = 0;
                        for (int i = 0; i < LUTS.faceOffsets.Count; i++)
                        {
                            var check = new Vector3Int(x, y, z) + LUTS.faceOffsets[i];
                            if (voxels.Contains(check))
                            {
                                if (voxels[check].depth == fillVoxelValue)
                                {
                                    fillCount++;
                                }
                            }
                        }

                        if (fillCount >= 2)
                        {
                            voxels[x,y,z] = new VoxelData() { depth = fillVoxelValue };
                        }
                    }
                }
            }
        }
    }
}
