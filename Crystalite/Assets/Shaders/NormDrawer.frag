#version 300 es
precision mediump float;

in vec4 Normal;
out vec4 FragColor;

void main(){
    FragColor = Normal;
}