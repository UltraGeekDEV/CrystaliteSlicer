#version 300 es
precision mediump float;

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;

uniform mat4 translation;
uniform mat4 rotation;
uniform mat4 scale;
uniform mat4 view;
uniform mat4 projection;
uniform vec3 col;

out vec3 Normal;
out vec4 Col;

void main(){
    gl_Position =  projection * view * translation * (scale * rotation * vec4(aPos,1.0));
    Normal = normalize(rotation * vec4(aNormal,1.0f)).xyz;
    Col = vec4(col,1);
}