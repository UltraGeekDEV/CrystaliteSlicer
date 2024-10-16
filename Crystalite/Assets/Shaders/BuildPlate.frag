#version 300 es
precision mediump float;

in vec3 Normal;
in vec4 Col;
out vec4 FragColor;

void main(){
    vec4 skyCol = vec4(0.2f,0.4f,0.5f,1.0f);
    float colorDominance = 0.0f;
    vec4 shadowMixRatio = (Col*colorDominance+vec4(1.0f-colorDominance))*(Col.r*0.299f+Col.g*0.587f+Col.b*0.114f);
    float ratio = dot(Normal,normalize(vec3(20,40,0)))*0.8f+0.2f;
    float specular = max(ratio*ratio -0.5f,0.0f)*4.0f;
    FragColor = Col*(ratio)+(shadowMixRatio*skyCol*(1.0f-ratio))+vec4(specular);
}