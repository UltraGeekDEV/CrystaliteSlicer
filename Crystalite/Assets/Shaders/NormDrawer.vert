#version 300 es
precision mediump float;

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;

uniform mat4 translation;
uniform mat4 rotation;
uniform mat4 scale;
uniform mat4 view;
uniform mat4 projection;
uniform mat4 sunProjection;
uniform vec3 col;

out vec4 Normal;

void main(){
    vec4 curPos = translation * (scale * rotation * vec4(aPos,1.0));
    gl_Position =  projection * view * curPos;
    Normal = vec4(normalize(rotation * vec4(aNormal,1.0f)).xyz,gl_Position.z*0.01f);
}