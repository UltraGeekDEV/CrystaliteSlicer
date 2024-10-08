using Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.ViewModels
{
    public class SettingsViewModel : ReactiveObject
    {
        public float ZResolution
        {
            get => Settings.Resolution.Z;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.resolution.Z, value);
        }

        public Vector3 PrintVolume
        {
            get => Settings.PrintVolume;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.printVolume, value);
        }

        public Vector3 Rotation
        {
            get => Settings.Rotation;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.rotation, value);
        }

        public Vector3 Scale
        {
            get => Settings.Scale;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.scale, value);
        }

        public Vector3 Offset
        {
            get => Settings.Offset;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.offset, value);
        }

        public float NozzleDiameter
        {
            get => Settings.NozzleDiameter;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.nozzleDiameter, value);
        }

        public float MaxSlope
        {
            get => Settings.MaxSlope;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.maxSlope, value);
        }

        public float MaxLayerHeight
        {
            get => Settings.MaxLayerHeight;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.maxLayerHeight, value);
        }

        public float HotendTemp
        {
            get => Settings.HotendTemp;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.hotendTemp, value);
        }

        public float BedTemp
        {
            get => Settings.BedTemp;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.bedTemp, value);
        }

        public string FanSpeed
        {
            get => (Settings.FanSpeed * 100).ToString();
            set 
            {
                float result;
                if (value.Length > 0 && float.TryParse(value,out result))
                {
                    Settings.Instance.fanSpeed = Math.Clamp(result, 0,100) / 100;
                }
                else if(value.Length == 0)
                {
                    Settings.Instance.fanSpeed = 0;
                }
                this.RaiseAndSetIfChanged(ref Settings.Instance.fanSpeed, Settings.Instance.fanSpeed * 100);
                this.RaiseAndSetIfChanged(ref Settings.Instance.fanSpeed, Settings.Instance.fanSpeed / 100);
            }
        }

        public string ExtrusionMult
        {
            get => (Settings.ExtrusionMult * 100).ToString();
            set
            {
                float result;
                if (value.Length > 0 && float.TryParse(value, out result))
                {
                    Settings.Instance.extrusionMult = Math.Max(result, 0) / 100;
                }
                else if (value.Length == 0)
                {
                    Settings.Instance.extrusionMult = 0;
                }
                this.RaiseAndSetIfChanged(ref Settings.Instance.extrusionMult, Settings.Instance.extrusionMult * 100);
                this.RaiseAndSetIfChanged(ref Settings.Instance.extrusionMult, Settings.Instance.extrusionMult / 100);
            }
        }

        public float OuterWallSpeed
        {
            get => Settings.OuterWallSpeed;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.outerWallSpeed, value);
        }

        public float InnerWallSpeed
        {
            get => Settings.InnerWallSpeed;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.innerWallSpeed, value);
        }

        public float InfillSpeed
        {
            get => Settings.InfillSpeed;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.infillSpeed, value);
        }

        public string InfillDensity
        {
            get => ((int)(Settings.InfillDensity * 100)).ToString();
            set
            {
                float result;
                if (value.Length > 0 && float.TryParse(value, out result))
                {
                    Settings.Instance.infillDensity = Math.Clamp(result, 0.001f, 100) / 100;
                }
                else if (value.Length == 0)
                {
                    Settings.Instance.infillDensity = 0;
                }
                this.RaiseAndSetIfChanged(ref Settings.Instance.infillDensity, Settings.Instance.infillDensity * 100);
                this.RaiseAndSetIfChanged(ref Settings.Instance.infillDensity, Settings.Instance.infillDensity / 100);
            }
        }

        public int WallCount
        {
            get => Settings.WallCount;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.wallCount, value);
        }

        public float SmoothingAngle
        {
            get => Settings.SmoothingAngle;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.smoothingAngle, value);
        }

        public int SmoothingCount
        {
            get => Settings.SmoothingCount;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.smoothingCount, value);
        }

        public float TopThickness
        {
            get => Settings.TopThickness;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.topThickness, value);
        }

        public float TravelSpeed
        {
            get => Settings.TravelSpeed;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.travelSpeed, value);
        }

        public float RetractionDistance
        {
            get => Settings.RetractionDistance;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.retractionDistance, value);
        }

        public float RetractionSpeed
        {
            get => Settings.RetractionSpeed;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.retractionSpeed, value);
        }

        public float OverhangOverlap
        {
            get => Settings.OverhangOverlap;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.overhangOverlap, value);
        }

        public bool TimelapseEnabled
        {
            get => Settings.TimelapseEnabled;
            set => this.RaiseAndSetIfChanged(ref Settings.Instance.timelapseEnabled, value);
        }
    }
}
