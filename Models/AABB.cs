using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class AABB
    {
        public Vector3 upperRight;
        public Vector3 lowerLeft;

        public bool Intersects(AABB other)
        {
            return
                upperRight.X >= other.lowerLeft.X && lowerLeft.X <= other.upperRight.X &&
                upperRight.Y >= other.lowerLeft.Y && lowerLeft.Y <= other.upperRight.Y &&
                upperRight.Z >= other.lowerLeft.Z && lowerLeft.Z <= other.upperRight.Z;
        }
        //https://michael-schwarz.com/research/publ/files/vox-siga10.pdf
        public bool Intersects(Triangle tri)
        {
            if (!Intersects(tri.GetAABB()))
            {
                return false;
            }

            var norm = tri.GetNormal();

            var boxSize = upperRight-lowerLeft;

            var c = new Vector3(
                norm.X > 0.0f? boxSize.X : 0.0f,
                norm.Y > 0.0f ? boxSize.Y : 0.0f,
                norm.Z > 0.0f ? boxSize.Z : 0.0f);

            var dp1 = Vector3.Dot(norm,c - tri.a);
            var dp2 = Vector3.Dot(norm, boxSize - c - tri.a);

            if ((Vector3.Dot(norm,lowerLeft)+dp1) * (Vector3.Dot(norm,lowerLeft)+dp2) > 0.0f)
            {
                return false;
            }

            var xym = (norm.Z < 0.0f ? -1.0f : 1.0f);
            var ne0xy = new Vector2(-(tri.b - tri.a).Y, (tri.b - tri.a).X)*xym;
            var ne1xy = new Vector2(-(tri.c - tri.b).Y, (tri.c - tri.b).X)*xym;
            var ne2xy = new Vector2(-(tri.a - tri.c).Y, (tri.a - tri.c).X)*xym;

            var v0xy = new Vector2(tri.a.X, tri.a.Y);
            var v1xy = new Vector2(tri.b.X, tri.b.Y);
            var v2xy = new Vector2(tri.c.X, tri.c.Y);

            var de0xy = -Vector2.Dot(ne0xy, v0xy) + Math.Max(0.0f, boxSize.X * ne0xy.X) + Math.Max(0.0f, boxSize.Y * ne0xy.Y);
            var de1xy = -Vector2.Dot(ne1xy, v1xy) + Math.Max(0.0f, boxSize.X * ne1xy.X) + Math.Max(0.0f, boxSize.Y * ne1xy.Y);
            var de2xy = -Vector2.Dot(ne2xy, v2xy) + Math.Max(0.0f, boxSize.X * ne2xy.X) + Math.Max(0.0f, boxSize.Y * ne2xy.Y);

            var pxy = new Vector2(lowerLeft.X, lowerLeft.Y);

            if(Vector2.Dot(ne0xy,pxy)+de0xy < 0.0f 
                || Vector2.Dot(ne1xy,pxy)+de1xy < 0.0f
                || Vector2.Dot(ne2xy,pxy)+de2xy < 0.0f)
            {
                return false;
            }

            var yzm = (norm.Z < 0.0f ? -1.0f : 1.0f);
            var ne0yz = new Vector2(-(tri.b - tri.a).Z, (tri.b - tri.a).Y) * yzm;
            var ne1yz = new Vector2(-(tri.c - tri.b).Z, (tri.c - tri.b).Y) * yzm;
            var ne2yz = new Vector2(-(tri.a - tri.c).Z, (tri.a - tri.c).Y) * yzm;

            var v0yz = new Vector2(tri.a.Y, tri.a.Z);
            var v1yz = new Vector2(tri.b.Y, tri.b.Z);
            var v2yz = new Vector2(tri.c.Y, tri.c.Z);

            var de0yz = -Vector2.Dot(ne0yz, v0yz) + Math.Max(0.0f, boxSize.Y * ne0yz.X) + Math.Max(0.0f, boxSize.Z * ne0yz.Y);
            var de1yz = -Vector2.Dot(ne1yz, v1yz) + Math.Max(0.0f, boxSize.Y * ne1yz.X) + Math.Max(0.0f, boxSize.Z * ne1yz.Y);
            var de2yz = -Vector2.Dot(ne2yz, v2yz) + Math.Max(0.0f, boxSize.Y * ne2yz.X) + Math.Max(0.0f, boxSize.Z * ne2yz.Y);

            var pyz = new Vector2(lowerLeft.Y, lowerLeft.Z);

            if (Vector2.Dot(ne0yz, pyz) + de0yz < 0.0f
                || Vector2.Dot(ne1yz, pyz) + de1yz < 0.0f
                || Vector2.Dot(ne2yz, pyz) + de2yz < 0.0f)
            {
                return false;
            }

            var zxm = (norm.X < 0.0f ? -1.0f : 1.0f);
            var ne0zx = new Vector2(-(tri.b - tri.a).X, (tri.b - tri.a).Z) * zxm;
            var ne1zx = new Vector2(-(tri.c - tri.b).X, (tri.c - tri.b).Z) * zxm;
            var ne2zx = new Vector2(-(tri.a - tri.c).X, (tri.a - tri.c).Z) * zxm;

            var v0zx = new Vector2(tri.a.Z, tri.a.X);
            var v1zx = new Vector2(tri.b.Z, tri.b.X);
            var v2zx = new Vector2(tri.c.Z, tri.c.X);

            var de0zx = -Vector2.Dot(ne0zx, v0zx) + Math.Max(0.0f, boxSize.Z * ne0zx.X) + Math.Max(0.0f, boxSize.X * ne0zx.Y);
            var de1zx = -Vector2.Dot(ne1zx, v1zx) + Math.Max(0.0f, boxSize.Z * ne1zx.X) + Math.Max(0.0f, boxSize.X * ne1zx.Y);
            var de2zx = -Vector2.Dot(ne2zx, v2zx) + Math.Max(0.0f, boxSize.Z * ne2zx.X) + Math.Max(0.0f, boxSize.X * ne2zx.Y);

            var pzx = new Vector2(lowerLeft.Z, lowerLeft.X);

            if (Vector2.Dot(ne0zx, pzx) + de0zx < 0.0f
                || Vector2.Dot(ne1zx, pzx) + de1zx < 0.0f
                || Vector2.Dot(ne2zx, pzx) + de2zx < 0.0f)
            {
                return false;
            }

            return true;
        }
    }
    public static class AABBRelatedExtensions
    {
        public static AABB GetAABB(this Triangle tri)
        {
            var aabb = new AABB();
            aabb.lowerLeft.X = Math.Min(tri.c.X, Math.Min(tri.a.X, tri.b.X));
            aabb.lowerLeft.Y = Math.Min(tri.c.Y, Math.Min(tri.a.Y, tri.b.Y));
            aabb.lowerLeft.Z = Math.Min(tri.c.Z, Math.Min(tri.a.Z, tri.b.Z));

            aabb.upperRight.X = Math.Max(tri.c.X, Math.Max(tri.a.X, tri.b.X));
            aabb.upperRight.Y = Math.Max(tri.c.Y, Math.Max(tri.a.Y, tri.b.Y));
            aabb.upperRight.Z = Math.Max(tri.c.Z, Math.Max(tri.a.Z, tri.b.Z));

            return aabb;
        }
    }
}
