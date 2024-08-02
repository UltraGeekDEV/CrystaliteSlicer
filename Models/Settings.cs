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
        public static float DirectionChangeWeight { get => Instance.directionChangeWeight; set => Instance.directionChangeWeight = value; }
        public static float DistanceWeight { get => Instance.distanceWeight; set => Instance.distanceWeight = value; }
        public static float RandomWeight { get => Instance.randomWeight; set => Instance.randomWeight = value; }
        public static int AntCount { get => Instance.antCount; set => Instance.antCount = value; }
        public static int StepCount { get => Instance.stepCount; set => Instance.stepCount = value; }

        //model settings -to be removed-
        private Vector3 rotation;
        private Vector3 scale;
        private Vector3 offset;

        //printer settings
        private Vector3 printVolume;
        private float nozzleDiameter;
        private float maxSlope;
        private float maxLayerHeight;

        //print settings
        private Vector3 resolution;
        private float hotendTemp;
        private float bedTemp;
        private float fanSpeed;
        private float extrusionMult;
        private float outerWallSpeed;
        private float innerWallSpeed;
        private float infillSpeed;
        private float infillDensity;
        private int wallCount;

        //toolpath settings
        private float pheromoneWeight;
        private float directionChangeWeight;
        private float distanceWeight;
        private float randomWeight;
        private int antCount;
        private int stepCount;
    }
}
