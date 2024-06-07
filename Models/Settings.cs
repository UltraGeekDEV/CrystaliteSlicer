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

        public Vector3 Resolution { get => resolution; set => resolution = value; }
        public Vector3 PrintVolume { get => printVolume; set => printVolume = value; }
        public Vector3 Rotation { get => rotation; set => rotation = value; }
        public Vector3 Scale { get => scale; set => scale = value; }
        public Vector3 Offset { get => offset; set => offset = value; }
        public float NozzleDiameter { get => nozzleDiameter; set => nozzleDiameter = value; }
        public float MaxSlope { get => maxSlope; set => maxSlope = value; }
        public float MaxLayerHeight { get => maxLayerHeight; set => maxLayerHeight = value; }
        public float HotendTemp { get => hotendTemp; set => hotendTemp = value; }
        public float BedTemp { get => bedTemp; set => bedTemp = value; }
        public float FanSpeed { get => fanSpeed; set => fanSpeed = value; }
        public float ExtrusionMult { get => extrusionMult; set => extrusionMult = value; }
        public float OuterWallSpeed { get => outerWallSpeed; set => outerWallSpeed = value; }
        public float InnerWallSpeed { get => innerWallSpeed; set => innerWallSpeed = value; }
        public float InfillSpeed { get => infillSpeed; set => infillSpeed = value; }
        public float InfillDensity { get => infillDensity; set => infillDensity = value; }

        private Vector3 resolution;
        private Vector3 printVolume;

        private Vector3 rotation;
        private Vector3 scale;
        private Vector3 offset;

        private float nozzleDiameter;
        private float maxSlope;
        private float maxLayerHeight;
        private float infillDensity;

        private float hotendTemp;
        private float bedTemp;
        private float fanSpeed;
        private float extrusionMult;
        private float outerWallSpeed;
        private float innerWallSpeed;
        private float infillSpeed;
    }
}
