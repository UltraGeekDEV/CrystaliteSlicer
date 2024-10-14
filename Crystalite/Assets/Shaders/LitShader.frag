#version 300 es
precision mediump float;

in vec3 Normal;
in vec4 Col;
out vec4 FragColor;

void main(){
    float ratio = dot(Normal,normalize(vec3(5,10,5)))*0.5+0.5;
    FragColor = Col*(ratio)+vec4(0.1f,0.2f,0.3f,1.0f)*(1.0f-ratio);
}