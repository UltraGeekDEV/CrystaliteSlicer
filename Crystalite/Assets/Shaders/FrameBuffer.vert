#version 300 es
precision mediump float;

layout (location = 0) in vec3 aPos;

out vec2 uv;

void main()
{
    gl_Position = vec4(aPos.x, aPos.y, aPos.z, 1.0);
    uv = vec2(aPos.x,aPos.y)*0.5f+0.5f;
}