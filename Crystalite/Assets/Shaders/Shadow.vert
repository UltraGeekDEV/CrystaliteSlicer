#version 300 es
precision mediump float;

layout (location = 0) in vec3 aPos;

uniform mat4 translation;
uniform mat4 rotation;
uniform mat4 scale;
uniform mat4 view;
uniform mat4 projection;
uniform mat4 sun;

void main(){
    gl_Position =  sun * translation * (scale * rotation * vec4(aPos,1.0));
}