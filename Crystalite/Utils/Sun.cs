using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public static class Sun
    {
        public static Vector3 pos;
        public static Vector3 dir;
        public static Vector3 target;
        public static Matrix4x4 view;
        public static Matrix4x4 Projection;

        public static void Setup()
        {
            pos = Vector3.Normalize(new Vector3(20, 80, 0)) * Settings.PrintVolume.Length()*0.05f;
            target = new Vector3(Settings.PrintVolume.X,0, -Settings.PrintVolume.Y) * new Vector3(0.05f, 0.0f, 0.05f);
            dir = pos - target;
            view = Matrix4x4.CreateLookAt(pos, target, Vector3.UnitY);
            Projection = Matrix4x4.CreateOrthographic((Settings.PrintVolume *new Vector3(1.0f,0.0f,1.0f)).Length()*0.1f, (Settings.PrintVolume * new Vector3(1.0f, 0.0f, 1.0f)).Length() * 0.1f, 0.01f,Settings.PrintVolume.Length()*0.15f);
        }
    }
}
