using CrystaliteSlicer.GcodeGeneration;
using CrystaliteSlicer.Postprocessing;
using CrystaliteSlicer.V2;
using Models;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace CrystaliteSlicer.Tests
{
    public class EndToEndTests
    {
        private readonly ITestOutputHelper output;
        public EndToEndTests(ITestOutputHelper output) => this.output = output;

        private static void UseSettings(float infillDensity)
        {
            Settings.Instance = new Settings
            {
                printVolume = new Vector3(235, 235, 245),
                nozzleDiameter = 0.4f,
                maxSlope = 10,
                maxLayerHeight = 0.3f,
                resolution = new Vector3(0.2f, 0.2f, 0.05f),
                hotendTemp = 200.5f,
                bedTemp = 60.5f,
                fanSpeed = 1,
                extrusionMult = 1,
                outerWallSpeed = 33.3333f,
                innerWallSpeed = 35,
                infillSpeed = 50,
                infillDensity = infillDensity,
                travelSpeed = 150,
                wallCount = 3,
                topThickness = 1,
                retractionDistance = 4,
                retractionSpeed = 45,
            };
        }

        private List<string> SliceToGcode(List<Triangle> mesh, float infillDensity, out SliceResult result)
        {
            UseSettings(infillDensity);
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("hu-HU");
            try
            {
                result = new VSlicer(SliceOptions.FromSettings()).Slice(mesh);
                output.WriteLine($"{result.LayerCount} layers, stalled={result.Stalled}; {result.Timings}");
                var path = new PurgeLine().Process(result.Path).ToList();
                return new MarlinGCodeGenerator().GetGcode(path);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static readonly Regex Word = new(@"([XYZEFS])(-?[0-9.]+)");

        /// <summary>Filament volume pushed while moving in XY (skips the purge line, which starts at x=5).</summary>
        private static double ExtrudedVolume(List<string> gcode, out float minZ, out float maxZ)
        {
            double e = 0, x = 0, y = 0, z = 0, volume = 0;
            minZ = float.MaxValue; maxZ = float.MinValue;
            int layerSeen = 0;
            foreach (var line in gcode)
            {
                if (line.StartsWith(";LAYER:")) layerSeen++;
                if (!line.StartsWith("G0") && !line.StartsWith("G1")) continue;
                double nx = x, ny = y, nz = z, ne = e;
                bool hasE = false;
                foreach (Match m in Word.Matches(line))
                {
                    double v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    switch (m.Groups[1].Value)
                    {
                        case "X": nx = v; break;
                        case "Y": ny = v; break;
                        case "Z": nz = v; break;
                        case "E": ne = v; hasE = true; break;
                    }
                }
                bool moves = Math.Abs(nx - x) + Math.Abs(ny - y) > 1e-9;
                if (hasE && moves && ne > e && layerSeen > 0)
                {
                    volume += (ne - e) * Math.PI * 1.75 * 1.75 / 4;
                    minZ = MathF.Min(minZ, (float)nz);
                    maxZ = MathF.Max(maxZ, (float)nz);
                }
                x = nx; y = ny; z = nz;
                if (hasE) e = ne;
            }
            return volume;
        }

        [Fact]
        public void GcodeIsWellFormedOnCommaLocales()
        {
            var gcode = SliceToGcode(TestMeshes.TShape(), 0.2f, out _);
            Assert.DoesNotContain(gcode, l => !l.StartsWith(";") && l.Contains(','));
            Assert.DoesNotContain(gcode, l => Regex.IsMatch(l, @"\d[eE][-+]?\d|NaN|∞|Infinity"));
            Assert.Contains("M140 S60.5", gcode);
            Assert.Contains(gcode, l => Regex.IsMatch(l, @" F1999\.99\d*$"));
            int count = int.Parse(gcode.First(l => l.StartsWith(";LAYER_COUNT:")).Split(':')[1]);
            Assert.Equal(count, gcode.Count(l => l.StartsWith(";LAYER:")));
        }

        [Theory]
        [InlineData("box")]
        [InlineData("cylinder")]
        [InlineData("ramp")]
        public void SolidPartExtrudesItsOwnVolume(string shape)
        {
            var mesh = shape switch
            {
                "box" => TestMeshes.Box(new Vector3(60, 60, 5), new Vector3(75, 72, 10)), // floating: must be dropped to the bed
                "cylinder" => TestMeshes.Cylinder(new Vector2(80, 80), 8, 0, 8),
                _ => TestMeshes.Ramp(40, 70, 40, 52, 2, 2 + 30 * MathF.Tan(8 * MathF.PI / 180)),
            };
            var gcode = SliceToGcode(mesh, 1.0f, out var result);
            double volume = ExtrudedVolume(gcode, out float minZ, out float maxZ);
            double expected = Math.Abs(TestMeshes.SignedVolume(mesh));
            float height = mesh.Max(t => MathF.Max(t.a.Z, MathF.Max(t.b.Z, t.c.Z))) - mesh.Min(t => MathF.Min(t.a.Z, MathF.Min(t.b.Z, t.c.Z)));
            output.WriteLine($"{shape}: extruded {volume:F0} mm³ vs model {expected:F0} mm³ ({volume / expected:P1}); Z {minZ:F3}..{maxZ:F3} (height {height:F3}); {gcode.Count} lines");
            Assert.False(result.Stalled);
            Assert.InRange(volume / expected, 0.85, 1.12);
            Assert.True(minZ > 0.04f && minZ <= 0.31f, $"first layer at {minZ}");
            // The highest bead centre is the outer wall, inset half a line width from a possibly sloped edge.
            Assert.InRange(maxZ, height - 0.4f * MathF.Tan(8 * MathF.PI / 180) - 0.005f, height + 1e-3f);
        }
    }
}
