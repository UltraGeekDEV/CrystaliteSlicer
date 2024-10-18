using Crystalite.Models;
using Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public class MeshData
    {
        public static MeshData? instance;
        public Mesh? selected;

        public AxisMesh? activeAxis;

        public List<Mesh> models = new List<Mesh>();
        public List<Mesh> staticUI = new List<Mesh>();
        public List<Mesh> translationHandles = new List<Mesh>();

        public List<List<Mesh>> objectPass = new List<List<Mesh>>();
        public List<List<Mesh>> goochPass = new List<List<Mesh>>();
        public List<List<Mesh>> UIPass = new List<List<Mesh>>();
        public MeshData()
        {
            objectPass.Add(staticUI);
            objectPass.Add(models);

            goochPass.Add(models);
        }
        public void ReleaseAxis()
        {
            if (activeAxis != null)
            {
                activeAxis.ResetCol();
                activeAxis = null;
            }
        }
        public void CastRay(Vector3 dir,Vector3 start)
        {
            var uiHits = translationHandles.Select(x => x.RayCast(dir, start)).Where(x => x.Item1 >= 0).ToList();

            if (uiHits.Count() > 0)
            {
                activeAxis = (AxisMesh)uiHits.MinBy(x => x.Item1).Item2;
                activeAxis.col = new OpenTK.Mathematics.Vector3(Math.Min(activeAxis.col.X+0.4f,1.0f), Math.Min(activeAxis.col.Y + 0.4f, 1.0f), Math.Min(activeAxis.col.Z + 0.4f, 1.0f));
            }
            else
            {
                if (selected != null)
                {
                    selected.hasOutline = false;
                    selected = null;
                    DisableTranslationHandles();
                }

                var modelHits = models.Select(x => x.RayCast(dir, start)).Where(x => x.Item1 >= 0).ToList();
                if (modelHits.Count() > 0)
                {
                    selected = modelHits.MinBy(x => x.Item1).Item2;
                    selected.hasOutline = true;
                    EnableTranslationHandles();
                }
            }
        }
        private void EnableTranslationHandles()
        {
            UIPass.Add(translationHandles);
            UpdateTranslationHandles();
        }
        public void UpdateTranslationHandles() 
        {
            foreach (var mesh in translationHandles)
            {
                var ll = Vector3.Transform(Vector3.Transform(Vector3.Transform(selected.com, selected.rotation), selected.scale), selected.translation) * new Vector3(1.0f, 0, 1.0f);
                var ur = Vector3.Transform(Vector3.Transform(Vector3.Transform(selected.upperRight, selected.rotation), selected.scale), selected.translation);
                ll.Y = ur.Y * 0.25f;
                mesh.translation = Matrix4x4.CreateTranslation(ll);
            }
        }

        private void DisableTranslationHandles()
        {
            UIPass.Remove(translationHandles);
        }
    }
}
