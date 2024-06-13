using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public static Vector3Int One { get => new Vector3Int(1,1,1); }

        public override int GetHashCode()
        {
            unchecked
            {
                if (Z == 0)
                {
                    int hash = 17;
                    hash = hash * 23 + X.GetHashCode();
                    hash = hash * 23 + Y.GetHashCode();
                    hash = hash * 23 + Z.GetHashCode();
                    return hash;
                }
                else
                {
                    int hash = 17;
                    hash = hash * 23 + X.GetHashCode();
                    hash = hash * 23 + Y.GetHashCode();
                    return hash;
                }
            }
        }
        public Vector3Int()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }
        public Vector3Int(Vector3 vec)
        {
            X = (int)Math.Round(vec.X);
            Y = (int)Math.Round(vec.Y);
            Z = (int)Math.Round(vec.Z);
        }
        public Vector3Int(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        public Vector3Int(int x, int y)
        {
            X = x;
            Y = y;
            Z = 0;
        }

        public static Vector3Int operator +(Vector3Int lhs, Vector3Int rhs)
        {
            return new Vector3Int(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
        }
        public static Vector3 operator +(Vector3 lhs, Vector3Int rhs)
        {
            return new Vector3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
        }
        public static Vector3 operator +(Vector3Int lhs, Vector3 rhs)
        {
            return new Vector3(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
        }
        public static Vector3Int operator -(Vector3Int lhs, Vector3Int rhs)
        {
            return new Vector3Int(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
        }
        public static Vector3 operator *(Vector3Int lhs, Vector3 rhs)
        {
            return new Vector3(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);
        }
        public static Vector3Int operator *(Vector3Int lhs, int rhs)
        {
            return new Vector3Int(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);
        }
        public static Vector3Int operator /(Vector3Int lhs, int rhs)
        {
            return new Vector3Int(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);
        }

        public static Vector3 operator *(Vector3Int lhs, float rhs)
        {
            return new Vector3(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);
        }
        public static Vector3 operator /(Vector3Int lhs, float rhs)
        {
            return new Vector3(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);
        }

        public static Vector3 operator /(Vector3Int lhs, Vector3 rhs)
        {
            return new Vector3(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);
        }

        public static implicit operator Vector3Int(Vector3 vec) => new Vector3Int(vec);

        public double SQRMagnitude()
        {
            return X * X + Y * Y + Z * Z;
        }

        public double Magnitude()
        {
            return Math.Sqrt(SQRMagnitude());
        }

        public bool Equals(Vector3Int other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }
        public override string ToString()
        {
            return $"{X}&{Y}&{Z}";
        }

        public float Dot(Vector3Int other)
        {
            return X * X + Y * Y + Z * Z;
        }

        internal Vector3Int Abs()
        {
            return new Vector3Int(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));
        }

        public static Vector3Int Cross(Vector3Int a, Vector3Int b)
        {
            return new Vector3Int(a.Y * b.Z - a.Z * b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        }
        public Vector3Int Normalize()
        {
            if (Math.Abs(X) > Math.Abs(Y))
            {
                if(Math.Abs(X) > Math.Abs(Z))
                {
                    return new Vector3Int(Math.Sign(X), 0, 0);
                }
                else
                {
                    return new Vector3Int(0, 0, 0);
                }
            }
            else
            {
                if (Math.Abs(Y) > Math.Abs(Z))
                {
                    return new Vector3Int(0, Math.Sign(Y), 0);
                }
                else
                {
                    return new Vector3Int(0, 0, 0);
                }
            }
        }
    }
}
