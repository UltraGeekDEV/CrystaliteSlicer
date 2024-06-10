using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public static class Utils
    {
        public static Vector3Int CompareAndSwap(Vector3Int A, Vector3Int B, Func<int, int, bool> predicate)
        {
            if (predicate(A.X, B.X))
            {
                A.X = B.X;
            }
            if (predicate(A.Y, B.Y))
            {
                A.Y = B.Y;
            }
            if (predicate(A.Z, B.Z))
            {
                A.Z = B.Z;
            }
            return A;
        }

        public static Vector3 CompareAndSwap(Vector3 A, Vector3 B, Func<float, float, bool> predicate)
        {
            if (predicate(A.X, B.X))
            {
                A.X = B.X;
            }
            if (predicate(A.Y, B.Y))
            {
                A.Y = B.Y;
            }
            if (predicate(A.Z, B.Z))
            {
                A.Z = B.Z;
            }
            return A;
        }

        public static Vector3Int GetID(Vector3 pos)
        {
            return (pos / Settings.Resolution);
        }

        public static Vector3 GetPos(Vector3Int id)
        {
            return id*Settings.Resolution;
        }
        public static AABB GetAABB(Vector3Int id)
        {
            return new AABB() {lowerLeft = GetPos(id)-Settings.Resolution*0.5f,upperRight = GetPos(id) + Settings.Resolution * 0.5f };
        }
    }
}
