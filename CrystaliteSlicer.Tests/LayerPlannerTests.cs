using CrystaliteSlicer.V2.Dexels;
using CrystaliteSlicer.V2.Planning;
using Models;
using System.Numerics;
using Xunit.Abstractions;

namespace CrystaliteSlicer.Tests
{
    public class LayerPlannerTests
    {
        private readonly ITestOutputHelper output;
        public LayerPlannerTests(ITestOutputHelper output) => this.output = output;

        private static readonly PlannerSettings Defaults = new()
        {
            MaxLayerHeight = 0.3f,
            MinLayerHeight = 0.05f,
            Slope = MathF.Tan(10 * MathF.PI / 180),
            SupportDistance = 0.4f,
        };

        private (List<LayerPlan> layers, LayerPlanner planner, List<float[]> clearanceBefore) Plan(List<Triangle> mesh, float cell = 0.2f)
        {
            var grid = DexelGrid.Build(mesh, cell);
            var planner = new LayerPlanner(grid, Defaults);
            var layers = new List<LayerPlan>();
            var clearances = new List<float[]>();
            var before = (float[])planner.Clearance.Clone();
            LayerPlan? layer;
            while ((layer = planner.NextLayer()) != null)
            {
                layers.Add(layer);
                clearances.Add(before);
                before = (float[])planner.Clearance.Clone();
                Assert.True(layers.Count < 10000, "runaway planner");
            }
            output.WriteLine($"{layers.Count} layers, stalled={planner.Stalled}, unprinted={planner.UnprintedVolume():F3} mm³ of {grid.Volume():F1}");
            foreach (var line in planner.DescribeUnfinished(8))
            {
                output.WriteLine("  unfinished " + line);
            }
            return (layers, planner, clearances);
        }

        private static void AssertLayerInvariants(List<LayerPlan> layers, LayerPlanner planner, List<float[]> clearanceBefore)
        {
            var grid = planner.Grid;
            var rng = new Random(1);
            for (int l = 0; l < layers.Count; l++)
            {
                var layer = layers[l];
                for (int a = 0; a < layer.Columns.Length; a++)
                {
                    int k = layer.Interval[a];
                    Assert.True(layer.Top[a] <= grid.ZOut[k] + 1e-5f);
                    Assert.True(layer.Bottom[a] >= grid.ZIn[k] - 1e-5f);
                    float t = layer.Top[a] - layer.Bottom[a];
                    Assert.True(t > 0 && t <= Defaults.MaxLayerHeight + 1e-4f, $"thickness {t}");
                    Assert.True(layer.Top[a] >= clearanceBefore[l][layer.Columns[a]] - 1e-3f, "printed below collision clearance");
                }
                // Within a layer the nozzle cone must clear every other point of the same layer.
                for (int s = 0; s < Math.Min(4000, layer.Columns.Length * 4); s++)
                {
                    int a = rng.Next(layer.Columns.Length), b = rng.Next(layer.Columns.Length);
                    int ca = layer.Columns[a], cb = layer.Columns[b];
                    float dx = (ca % grid.NX - cb % grid.NX) * grid.CellSize, dy = (ca / grid.NX - cb / grid.NX) * grid.CellSize;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float allowed = Defaults.Slope * d;
                    Assert.True(layer.Top[b] - layer.Top[a] <= allowed + 2e-3f, $"layer {l}: slope violated {layer.Top[b] - layer.Top[a]} > {allowed} at d={d}");
                }
            }
        }

        [Fact]
        public void BoxPrintsCompletelyInPlanarLayers()
        {
            var (layers, planner, before) = Plan(TestMeshes.Box(new Vector3(50, 50, 0), new Vector3(60, 60, 10)));
            Assert.False(planner.Stalled);
            Assert.True(planner.UnprintedVolume() < 1e-3);
            Assert.InRange(layers.Count, 34, 36);
            AssertLayerInvariants(layers, planner, before);
        }

        [Fact]
        public void OverhangShelfIsPrinted()
        {
            var (layers, planner, before) = Plan(TestMeshes.TShape());
            Assert.False(planner.Stalled);
            Assert.True(planner.UnprintedVolume() < 1.0);
            AssertLayerInvariants(layers, planner, before);
        }

        [Fact]
        public void HubWaitsForRimReachedOnlyThroughASpoke()
        {
            var (layers, planner, before) = Plan(TestMeshes.HubSpokeRim());
            Assert.False(planner.Stalled);
            Assert.True(planner.UnprintedVolume() < 1.0);
            AssertLayerInvariants(layers, planner, before);
        }

        [Fact]
        public void CavityRoofIsBridgedFromTheWalls()
        {
            var (layers, planner, before) = Plan(TestMeshes.HollowBox());
            Assert.False(planner.Stalled);
            Assert.True(planner.UnprintedVolume() < 1.0);
            AssertLayerInvariants(layers, planner, before);
        }

        [Fact]
        public void RampTopIsFollowedExactly()
        {
            // 6° ramp, under the 10° clearance: layers should end on the ramp, not stair-step.
            var mesh = TestMeshes.Ramp(40, 70, 40, 50, 3, 3 + 30 * MathF.Tan(6 * MathF.PI / 180));
            var (layers, planner, before) = Plan(mesh);
            Assert.False(planner.Stalled);
            Assert.True(planner.UnprintedVolume() < 1e-2);
            AssertLayerInvariants(layers, planner, before);
            // Every column's last bead ends exactly on the ramp: no Z quantization of the top surface.
            var grid = planner.Grid;
            var finalTop = new Dictionary<int, float>();
            foreach (var layer in layers)
            {
                for (int a = 0; a < layer.Columns.Length; a++)
                {
                    finalTop[layer.Columns[a]] = layer.Top[a];
                }
            }
            float tan = MathF.Tan(6 * MathF.PI / 180);
            foreach (var (col, top) in finalTop)
            {
                float x = grid.NodePosition(col % grid.NX, col / grid.NX).X;
                Assert.Equal(3 + (x - 40) * tan, top, 3);
            }
        }

        [Fact]
        public void CylinderPrintsCompletely()
        {
            var (layers, planner, before) = Plan(TestMeshes.Cylinder(new Vector2(50, 50), 10, 0, 15));
            Assert.False(planner.Stalled);
            Assert.True(planner.UnprintedVolume() < 1e-2);
            AssertLayerInvariants(layers, planner, before);
        }
    }
}
