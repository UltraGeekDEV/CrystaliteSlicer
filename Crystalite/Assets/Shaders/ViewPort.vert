#version 300 es
precision mediump float;

in vec3 aPos;
in vec3 aNormal;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform vec3 col;

out vec3 Normal;
out vec4 Col;

void main(){
    gl_Position =  projection * view * model * vec4(aPos,1.0);
    Normal = normalize(mat3(model)*aNormal);
    Col = vec4(col,1);
}