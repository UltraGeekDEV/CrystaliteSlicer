using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public class CameraData
    {
        public static CameraData instance;
        public Vector3 pos;
        public Vector3 eulerAngles;
        public Vector3 targetPos;
        public float dist = 10;
        public float sensitivity = 17.0f;

        public Vector3 Forward { get
            {
                float yaw = MathF.PI * eulerAngles.Y / 180.0f; // Yaw
                float pitch = MathF.PI * eulerAngles.X / 180.0f; // Pitch
                return Vector3.Normalize(new Vector3(
                        MathF.Cos(pitch) * MathF.Sin(yaw),
                        MathF.Sin(pitch),
                        MathF.Cos(pitch) * MathF.Cos(yaw)
                        ));
            }
        }

        static CameraData()
        {
            instance = new CameraData();
        }
        public CameraData()
        {
            targetPos = new Vector3(0,1,0);
            dist = 20;
            eulerAngles = new Vector3(25,45,0);
        }

        

        public static Matrix4x4 CreateViewMatrix()
        {
            // Convert degrees to radians
            float pitch = MathF.PI * instance.eulerAngles.X / 180.0f;
            float yaw = MathF.PI * instance.eulerAngles.Y / 180.0f;
            float roll = MathF.PI * instance.eulerAngles.Z / 180.0f;

            // Calculate the camera's position based on distance and rotation angles
            Vector3 cameraPosition = new Vector3(
                instance.targetPos.X + instance.dist * MathF.Cos(pitch) * MathF.Sin(yaw),
                instance.targetPos.Y + instance.dist * MathF.Sin(pitch),
                instance.targetPos.Z + instance.dist * MathF.Cos(pitch) * MathF.Cos(yaw)
            );

            // Create the view matrix
            return Matrix4x4.CreateLookAt(cameraPosition, instance.targetPos, Vector3.UnitY);
            //return new Matrix4(matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44);

        }
    }
}
