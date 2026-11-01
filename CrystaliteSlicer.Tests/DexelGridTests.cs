using CrystaliteSlicer.V2.Dexels;
using System.Numerics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CrystaliteSlicer.Tests
{
    public class DexelGridTests
    {
        [Theory]
        [InlineData(0.2f)]
        [InlineData(0.1f)]
        public void BoxVolumeAndExactHeights(float cell)
        {
            var mesh = TestMeshes.Box(new Vector3(50, 50, 0), new Vector3(60, 60, 7.37f));
            var grid = DexelGrid.Build(mesh, cell);

            Assert.Equal(TestMeshes.SignedVolume(mesh), grid.Volume(), 735 * 0.03);
            for (int k = 0; k < grid.IntervalTotal; k++)
            {
                Assert.Equal(0f, grid.ZIn[k], 4);
                Assert.Equal(7.37f, grid.ZOut[k], 4);
            }
        }

        [Fact]
        public void CylinderVolume()
        {
            var mesh = TestMeshes.Cylinder(new Vector2(50, 50), 10, 0, 20);
            var grid = DexelGrid.Build(mesh, 0.2f);
            var expected = TestMeshes.SignedVolume(mesh);
            Assert.InRange(grid.Volume(), expected * 0.995, expected * 1.005);
        }

        [Fact]
        public void CavityGivesTwoIntervals()
        {
            var mesh = TestMeshes.HollowBox();
            var grid = DexelGrid.Build(mesh, 0.2f);
            var i = (int)((50 - grid.OriginX) / grid.CellSize);
            var col = grid.Index(i, i);
            Assert.Equal(2, grid.ColumnStart[col + 1] - grid.ColumnStart[col]);
            Assert.Equal(TestMeshes.SignedVolume(mesh), grid.Volume(), 8000 * 0.02);
        }

        [Fact]
        public void OverlappingShellsAreUnioned()
        {
            var mesh = TestMeshes.TShape();
            var grid = DexelGrid.Build(mesh, 0.2f);
            // pillar 10x10x12 + shelf part outside pillar 8x10x4
            Assert.Equal(1200 + 320, grid.Volume(), 1520 * 0.03);
            for (int col = 0; col < grid.ColumnCount; col++)
            {
                Assert.True(grid.ColumnStart[col + 1] - grid.ColumnStart[col] <= 1);
            }
        }
    }
}
