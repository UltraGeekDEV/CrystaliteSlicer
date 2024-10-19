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

        public AxisMesh? activeTranslationAxis;
        public AxisMesh? activeRotationAxis;

        public List<Mesh> models = new List<Mesh>();
        public List<Mesh> staticUI = new List<Mesh>();
        public List<Mesh> translationHandles = new List<Mesh>();
        public List<Mesh> rotationHandles = new List<Mesh>();

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
            if (activeTranslationAxis != null)
            {
                activeTranslationAxis.ResetCol();
                activeTranslationAxis = null;
            }
            if (activeRotationAxis != null)
            {
                activeRotationAxis.ResetCol();
                activeRotationAxis = null;
            }
        }
        public void CastRay(Vector3 dir, Vector3 start)
        {
            var rotationHits = rotationHandles.Select(x => x.RayCast(dir, start)).Where(x => x.Item1 >= 0).ToList();

            if (selected != null && rotationHits.Count() > 0)
            {
                activeRotationAxis = (AxisMesh)rotationHits.MinBy(x => x.Item1).Item2;
                activeRotationAxis.col = new OpenTK.Mathematics.Vector3(Math.Min(activeRotationAxis.col.X + 0.4f, 1.0f), Math.Min(activeRotationAxis.col.Y + 0.4f, 1.0f), Math.Min(activeRotationAxis.col.Z + 0.4f, 1.0f));
                return;
            }

            var translationHits = translationHandles.Select(x => x.RayCast(dir, start)).Where(x => x.Item1 >= 0).ToList();

            if (selected != null && translationHits.Count() > 0)
            {
                activeTranslationAxis = (AxisMesh)translationHits.MinBy(x => x.Item1).Item2;
                activeTranslationAxis.col = new OpenTK.Mathematics.Vector3(Math.Min(activeTranslationAxis.col.X + 0.4f, 1.0f), Math.Min(activeTranslationAxis.col.Y + 0.4f, 1.0f), Math.Min(activeTranslationAxis.col.Z + 0.4f, 1.0f));
                return;
            }

            if (selected != null)
            {
                selected.hasOutline = false;
                selected = null;
                DisableHandles();
            }

            var modelHits = models.Select(x => x.RayCast(dir, start)).Where(x => x.Item1 >= 0).ToList();
            if (modelHits.Count() > 0)
            {
                selected = modelHits.MinBy(x => x.Item1).Item2;
                selected.hasOutline = true;
                EnableHandles();
            }

        }
        private void EnableHandles()
        {
            UIPass.Add(translationHandles);
            UIPass.Add(rotationHandles);
            UpdateTranslationHandles();
        }
        public void UpdateTranslationHandles() 
        {
            foreach (var mesh in translationHandles)
            {
                var rot = Vector3.Transform(Vector3.Transform(Vector3.Transform(Vector3.Zero, selected.rotation), selected.scale), selected.translation);
                mesh.translation = Matrix4x4.CreateTranslation(rot);
            }
            foreach (var mesh in rotationHandles)
            {
                var rot = Vector3.Transform(Vector3.Transform(Vector3.Transform(Vector3.Zero, selected.rotation), selected.scale), selected.translation);
                mesh.translation = Matrix4x4.CreateTranslation(rot);
            }
        }

        private void DisableHandles()
        {
            UIPass.Remove(translationHandles);
            UIPass.Remove(rotationHandles);
        }
    }
}
