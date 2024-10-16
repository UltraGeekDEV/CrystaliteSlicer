using Crystalite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public static class Shaders
    {
        public static Dictionary<ShaderType, Shader> shaders; 
        public static void Setup()
        {
            shaders = new Dictionary<ShaderType, Shader>()
            {
                {ShaderType.lit,new Shader("LitShader.frag","LitShader.vert")},
                {ShaderType.unlit,new Shader("UnlitShader.frag","UnlitShader.vert")},
                {ShaderType.frameBuffer,new Shader("FrameBuffer.frag","FrameBuffer.vert")},
                {ShaderType.antiAliasing,new Shader("FXAA.frag","FXAA.vert")},
                {ShaderType.outline,new Shader("Outlining.frag","Outlining.vert")},
                {ShaderType.buildPlate,new Shader("BuildPlate.frag","BuildPlate.vert")},
                {ShaderType.shadow,new Shader("Shadow.frag","Shadow.vert")},
            };
        }
    }
    public enum ShaderType
    {
        lit,
        unlit,
        frameBuffer,
        antiAliasing,
        outline,
        buildPlate,
        shadow
    }
}
