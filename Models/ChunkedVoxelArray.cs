using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Models
{
    public class ChunkedVoxelArray : IVoxelCollection
    {
        private IVoxelCollection fullResArray;
        private int chunkSize;
        private VoxelData[,,] chunks;
        public VoxelData this[Vector3Int pos] { get => this[pos.X,pos.Y,pos.Z]; set => this[pos.X, pos.Y, pos.Z] = value; }
        public VoxelData this[int x, int y, int z] { get => chunks[x, y, z]; set
            {
                if (chunks[x,y,z].Layer != value.Layer)
                {
                    for (int i = 0; i < chunkSize; i++)
                    {
                        for (int j = 0; j < chunkSize; j++)
                        {
                            var pos = new Vector3Int(x + i, y + j, z);
                            if (fullResArray.Contains(pos))
                            {
                                var voxel = fullResArray[pos];
                                voxel.Layer = value.Layer;
                                chunks[x,y,z] = voxel;
                            }
                        }
                    }
                }
                chunks[x, y, z] = value;
            }
        }

        private Vector3Int size;
        public Vector3Int Size { get => size; set => size = value; }
        private Vector3 lowerLeft;
        public Vector3 LowerLeft { get => lowerLeft; set => lowerLeft = value; }
        private int layerCount;
        public int LayerCount { get => layerCount; set => layerCount = value; }
        public ChunkedVoxelArray(IVoxelCollection fullResArray)
        {
            this.fullResArray = fullResArray;
            lowerLeft = fullResArray.LowerLeft;

            chunkSize = (int)(Settings.Resolution.X / Settings.NozzleDiameter);
            size = fullResArray.Size / chunkSize;
            size.Z = fullResArray.Size.Z;

            chunks = new VoxelData[size.X, size.Y, size.Z];

            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    for (int k = 0; k < size.Z; k++)
                    {
                        if (IsChunkActive(new Vector3Int(i,j,k)))
                        {
                            this[i, j, k] = VoxelData.GetSolid();
                        }
                        else
                        {
                            this[i, j, k] = VoxelData.GetAir();
                        }
                    }
                }
            }
        }

        private bool IsChunkActive(Vector3Int id)
        {
            for (int i = 0; i < chunkSize; i++)
            {
                for (int j = 0; j < chunkSize; j++)
                {
                    if (fullResArray.Contains(id + new Vector3Int(i, j, 0)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public bool Contains(Vector3Int id)
        {
            return WithinBounds(id) && this[id].Depth != IVoxelCollection.airVoxel;
        }

        public IEnumerable<Vector3Int> GetAllActiveVoxels()
        {
            throw new NotImplementedException();
        }

        public bool WithinBounds(Vector3Int id)
        {
            return id.X >= 0 && id.Y >= 0 && id.Z >= 0 && id.X < size.X && id.Y < size.Y && id.Z < size.Z;
        }
    }
}
