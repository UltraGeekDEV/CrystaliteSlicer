using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Models
{
    public class AxisMesh : Mesh
    {
        public Vector3 axis;
        public Matrix4x4 originalRotation;

        public AxisMesh(Vector3 axis, IEnumerable<Triangle> triangles, Utils.ShaderType shader, float scale = 0.1f, bool reorient = true) : base(triangles,shader,scale)
        {
            this.axis = axis;
            ResetCol();
            depthTest = false;
        }

        public void ResetCol()
        {
            col = new OpenTK.Mathematics.Vector3(Math.Min(axis.X+0.3f,1.0f), Math.Min(axis.Z + 0.3f, 1.0f), Math.Min(axis.Y + 0.3f, 1.0f));
            //col = new OpenTK.Mathematics.Vector3(axis.X*0.75f, axis.Z * 0.75f, axis.Y * 0.75f);
        }
    }
}
