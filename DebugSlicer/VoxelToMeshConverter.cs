using Assimp;
using Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DebugSlicer
{
    public static class VoxelLuts
    {
        public static readonly Vector3Int[] faceCheck = new Vector3Int[6]
                {
                new Vector3Int(0,0,-1),
                new Vector3Int(0,0,1),
                new Vector3Int(0,1,0),
                new Vector3Int(0,-1,0),
                new Vector3Int(-1,0,0),
                new Vector3Int(1,0,0)
                };

        public static readonly Vector3[] vertices = new Vector3[8]{
                new Vector3(0.0f,0.0f,0.0f),//0
                new Vector3(1.0f,0.0f,0.0f),//1
                new Vector3(1.0f,1.0f,0.0f),//2
                new Vector3(0.0f,1.0f,0.0f),//3
                new Vector3(0.0f,0.0f,1.0f),//4
                new Vector3(1.0f,0.0f,1.0f),//5
                new Vector3(1.0f,1.0f,1.0f),//6
                new Vector3(0.0f,1.0f,1.0f) //7
        };

        public static readonly int[,] tris = new int[6, 4]
        {
                {0,3,1,2},//Back face
                {5,6,4,7},//Front face
                {3,7,2,6},//Top face
                {1,5,0,4},//Bottom face
                {4,7,0,3},//Left face
                {1,2,5,6}//Right face
        };
    }
    public class VoxelToMeshConverter
    {
        public static IEnumerable<Triangle> GetVoxelMesh(IVoxelCollection voxels)
        {
            ConcurrentQueue<Triangle> result = new ConcurrentQueue<Triangle>();

            for (int x = 0; x < voxels.Size.X; x++)
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    for (int z = 0; z < voxels.Size.Z; z++)
                    {
                        var voxel = new Vector3Int(x, y, z);
                        if (voxels.Contains(voxel) && voxels[voxel].Layer != 0)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                if (!voxels.Contains(VoxelLuts.faceCheck[i] + voxel) || (voxels.Contains(VoxelLuts.faceCheck[i] + voxel) && voxels[VoxelLuts.faceCheck[i] + voxel].Layer == 0))
                                {
                                    result.Enqueue(new Triangle(voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 0]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 1]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 2]] * Settings.Resolution));
                                    result.Enqueue(new Triangle(voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 2]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 1]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 3]] * Settings.Resolution));
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }
        public static IEnumerable<Triangle> GetVoxelMesh(IVoxelCollection voxels,int layer)
        {
            ConcurrentQueue<Triangle> result = new ConcurrentQueue<Triangle>();

            for (int x = 0; x < voxels.Size.X; x++)
            {
                for (int y = 0; y < voxels.Size.Y; y++)
                {
                    for (int z = 0; z < voxels.Size.Z; z++)
                    {
                        var voxel = new Vector3Int(x,y,z);
                        if (voxels.Contains(voxel) && voxels[voxel].Layer % layer == 0)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                if (!voxels.Contains(VoxelLuts.faceCheck[i] + voxel) || (voxels.Contains(VoxelLuts.faceCheck[i] + voxel) && voxels[VoxelLuts.faceCheck[i] + voxel].Layer % layer != 0))
                                {
                                    result.Enqueue(new Triangle(voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 0]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 1]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 2]] * Settings.Resolution));
                                    result.Enqueue(new Triangle(voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 2]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 1]] * Settings.Resolution, voxel * Settings.Resolution + VoxelLuts.vertices[VoxelLuts.tris[i, 3]] * Settings.Resolution));
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
