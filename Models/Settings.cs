using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Models
{
    public class Settings
    {
        [JsonIgnore]
        private static Settings instance;
        [JsonIgnore]
        public static Settings Instance { get
            {
                if(instance == null)
                {
                    instance = new Settings();
                }
                return instance;
            } set => instance = value;}

        //Static getter
        public static Vector3 Resolution { get =>   Instance.resolution; set =>      Instance.resolution = value; }
        public static Vector3 PrintVolume { get =>  Instance.printVolume; set =>     Instance.printVolume = value; }
        public static Vector3 Rotation { get =>     Instance.rotation; set =>        Instance.rotation = value; }
        public static Vector3 Scale { get =>        Instance.scale; set =>           Instance.scale = value; }
        public static Vector3 Offset { get =>       Instance.offset; set =>          Instance.offset = value; }
        public static float NozzleDiameter { get => Instance.nozzleDiameter; set =>  Instance.nozzleDiameter = value; }
        public static float MaxSlope { get =>       Instance.maxSlope; set =>        Instance.maxSlope = value; }
        public static float MaxLayerHeight { get => Instance.maxLayerHeight; set =>  Instance.maxLayerHeight = value; }
        public static float HotendTemp { get =>     Instance.hotendTemp; set =>      Instance.hotendTemp = value; }
        public static float BedTemp { get =>        Instance.bedTemp; set =>         Instance.bedTemp = value; }
        public static float FanSpeed { get =>       Instance.fanSpeed; set =>        Instance.fanSpeed = value; }
        public static float ExtrusionMult { get =>  Instance.extrusionMult; set =>   Instance.extrusionMult = value; }
        public static float OuterWallSpeed { get => Instance.outerWallSpeed; set =>  Instance.outerWallSpeed = value; }
        public static float InnerWallSpeed { get => Instance.innerWallSpeed; set =>  Instance.innerWallSpeed = value; }
        public static float InfillSpeed { get =>    Instance.infillSpeed; set =>     Instance.infillSpeed = value; }
        public static float InfillDensity { get =>  Instance.infillDensity; set =>   Instance.infillDensity = value; }
        public static int WallCount { get => Instance.wallCount; set => Instance.wallCount = value; }
        public static float PheromoneWeight { get => Instance.pheromoneWeight; set => Instance.pheromoneWeight = value; }
        public static float SmoothingAngle { get => Instance.smoothingAngle; set => Instance.smoothingAngle = value; }
        public static int SmoothingCount { get => Instance.smoothingCount; set => Instance.smoothingCount = value; }
        public static float TopThickness { get => Instance.topThickness; set => Instance.topThickness = value; }
        public static float TravelSpeed { get => Instance.travelSpeed; set => Instance.travelSpeed = value; }
        public static float RetractionDistance { get => Instance.retractionDistance; set => Instance.retractionDistance = value; }
        public static float RetractionSpeed { get => Instance.retractionSpeed; set => Instance.retractionSpeed = value; }
        public static float OverhangOverlap { get => Instance.overhangOverlap; set => Instance.overhangOverlap = value; }
        public static bool TimelapseEnabled { get => Instance.timelapseEnabled; set => Instance.timelapseEnabled = value; }

        //model settings -to be removed-
        public Vector3 rotation;
        public Vector3 scale = Vector3.One;
        public Vector3 offset = new Vector3(30, 30, 0);

        //printer settings
        public Vector3 printVolume = new Vector3(235, 235, 245);
        public float nozzleDiameter = 0.4f;
        public float maxSlope = 10;
        public float maxLayerHeight = 0.32f;

        //print settings
        public Vector3 resolution = new Vector3(0.2f, 0.2f, 0.05f);
        public float hotendTemp = 200;
        public float bedTemp = 50;
        public float fanSpeed = 1;
        public float extrusionMult = 1;
        public float outerWallSpeed = 35;
        public float innerWallSpeed = 35;
        public float infillSpeed = 50;
        public float infillDensity = 0.2f;
        public float travelSpeed = 150;
        public int wallCount = 3;
        public float topThickness = 2;
        public float retractionDistance = 4;
        public float retractionSpeed = 45;

        //toolpath settings
        public float pheromoneWeight;
        public float smoothingAngle = 0.75f;
        public int smoothingCount = 6;
        public float overhangOverlap;

        //timelapse settings
        public bool timelapseEnabled;
    }
}
