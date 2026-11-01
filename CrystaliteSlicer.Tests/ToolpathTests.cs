using CrystaliteSlicer.V2;
using CrystaliteSlicer.V2.Toolpath;
using Models;
using System.Numerics;
using Xunit.Abstractions;

namespace CrystaliteSlicer.Tests
{
    public class ToolpathTests
    {
        private readonly ITestOutputHelper output;
        public ToolpathTests(ITestOutputHelper output) => this.output = output;

        private static SliceOptions Options(float cell = 0.2f) => new()
        {
            CellSize = cell,
            Planner = new() { MaxLayerHeight = 0.3f, MinLayerHeight = 0.05f, Slope = MathF.Tan(10 * MathF.PI / 180), SupportDistance = 0.4f },
            Toolpath = new() { LineWidth = 0.4f, WallCount = 3, TopBottomThickness = 1.0f, InfillDensity = 0.2f },
        };

        private List<(int layer, List<LayerPath> paths)> Run(List<Triangle> mesh, out SliceResult result, float cell = 0.2f)
        {
            var layers = new List<(int, List<LayerPath>)>();
            result = new VSlicer(Options(cell)).Slice(mesh, (plan, surface, paths) => layers.Add((plan.Index, paths)));
            output.WriteLine($"{result.LayerCount} layers, {result.Path.Count} lines, stalled={result.Stalled}, unprinted={result.UnprintedVolume:F3}; {result.Timings}");
            return layers;
        }

        [Theory]
        [InlineData(0.2f)]
        [InlineData(0.4f)]
        public void CylinderOuterWallIsExact(float cell)
        {
            var center = new Vector2(50, 50);
            var layers = Run(TestMeshes.Cylinder(center, 10, 0, 6), out _, cell);
            float worst = 0;
            int count = 0;
            foreach (var (_, paths) in layers)
            {
                foreach (var p in paths.Where(p => p.Role == PathRole.OuterWall))
                {
                    foreach (var pt in p.Points)
                    {
                        worst = MathF.Max(worst, MathF.Abs(Vector2.Distance(pt, center) - 9.8f));
                        count++;
                    }
                }
            }
            output.WriteLine($"cell {cell}: {count} outer wall points, worst radial error {worst * 1000:F2} µm");
            Assert.True(count > 0);
            Assert.True(worst < 0.01f, $"outer wall off by {worst} mm");
        }

        [Fact]
        public void BoxWallsHaveSharpExactCorners()
        {
            var layers = Run(TestMeshes.Box(new Vector3(50.05f, 50.05f, 0), new Vector3(60.05f, 60.05f, 3)), out _);
            foreach (var (_, paths) in layers)
            {
                var outer = paths.Where(p => p.Role == PathRole.OuterWall).ToList();
                Assert.Single(outer);
                var pts = outer[0].Points;
                Assert.Equal(50.25f, pts.Min(p => p.X), 3);
                Assert.Equal(59.85f, pts.Max(p => p.X), 3);
                Assert.Equal(50.25f, pts.Min(p => p.Y), 3);
                Assert.Equal(59.85f, pts.Max(p => p.Y), 3);
                // A sharp corner: some vertex within 5 µm of the exact corner of the inset ring.
                Assert.Contains(pts, p => Vector2.Distance(p, new Vector2(50.25f, 50.25f)) < 0.005f);
            }
        }

        [Fact]
        public void BoxHasSkinTopAndBottomAndInfillInBetween()
        {
            var layers = Run(TestMeshes.Box(new Vector3(50, 50, 0), new Vector3(60, 60, 6)), out _);
            int skinLayers = layers.Count(l => l.paths.Any(p => p.Role == PathRole.Skin));
            int infillLayers = layers.Count(l => l.paths.Any(p => p.Role == PathRole.Infill));
            output.WriteLine($"{layers.Count} layers, {skinLayers} with skin, {infillLayers} with infill");
            Assert.InRange(skinLayers, 6, 10); // ~1 mm top + 1 mm bottom at 0.3 mm
            Assert.True(infillLayers >= layers.Count - 10);
        }

        [Fact]
        public void OverhangAndCavityProduceWallsEverywhere()
        {
            foreach (var mesh in new[] { TestMeshes.TShape(), TestMeshes.HollowBox() })
            {
                var layers = Run(mesh, out var result);
                Assert.False(result.Stalled);
                Assert.All(layers, l => Assert.NotEmpty(l.paths));
                // Layers that only bridge inward over a cavity are fully interior: they get skin, not walls.
                var noWall = layers.Where(l => !l.paths.Any(p => p.Role == PathRole.OuterWall)).ToList();
                Assert.All(noWall, l => Assert.Contains(l.paths, p => p.Role == PathRole.Skin));
                Assert.True(noWall.Count <= layers.Count / 10);
            }
        }
    }
}
