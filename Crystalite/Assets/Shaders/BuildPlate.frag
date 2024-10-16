#version 300 es
precision mediump float;

in vec3 Pos;
in vec3 Normal;
in vec4 Col;
out vec4 FragColor;

float noise(float a){

    float remainder = abs(mod(a,1.0f));
    return 1.0f-abs(remainder-0.5f)*2.0f;
}

void main(){

    float posX = Pos.x*5.0f;
    float posZ = Pos.z*5.0f;

    //float sinX = ((sin(posX*0.25f+sin(posZ*1.0f)*0.8f)+1.1f)*0.1f)+((sin(posX*0.3f+posZ*0.7f+cos(posX*4.0f+posZ*2.0f)*1.0f)+1.0f)*0.1f);
    //float sinZ = ((sin(posZ+cos(posX*3.3530f-posZ*1.23f)*1.25f)+1.0f)*0.1f)+((sin(posX*0.6432f+posZ*0.321231f+sin(posX*2.0f+posZ*4.0f)*1.0f)+1.0f)*0.1f)-0.4f;

    float sinX = 1.75f*sqrt(pow((noise(posX+(0.2f+noise(posX*0.1f+posZ*0.05f)*0.1f)*3.5f)+1.0f)*0.5f,2.0f) + pow((noise(posZ+(0.5f+noise(posX*0.1f+posZ*0.2f)*0.2f)*1.5f)+1.0f)*0.5f,2.0f));
    posX = Pos.x*0.5f+Pos.z*0.2f;
    posZ = Pos.x*0.2f+Pos.z*0.7f;
    float sinZ = sqrt(pow((noise(posX+(0.2f+noise(posX*0.1f+posZ*0.05f)*0.1f)*3.5f)+1.0f)*0.5f,2.0f) + pow((noise(posZ+(0.5f+noise(posX*0.1f+posZ*0.2f)*0.2f)*1.5f)+1.0f)*0.5f,2.0f));
    float patternStrength =0.75f;
    vec3 norm = normalize(vec3(Normal.x+sinX*patternStrength,Normal.y,Normal.z+sinZ*patternStrength*0.75f));

    vec4 skyCol = vec4(0.2f,0.4f,0.5f,1.0f);
    float colorDominance = 0.5f;
    vec4 shadowMixRatio = (Col*colorDominance+vec4(1.0f-colorDominance))*(Col.r*0.299f+Col.g*0.587f+Col.b*0.114f);
    float ratio = dot(norm,normalize(vec3(20,40,0)))*0.8f+0.2f;
    float specular = max(ratio*ratio*ratio -0.6f,0.0f)*0.1f;
    FragColor = Col*(ratio)+(shadowMixRatio*skyCol*(1.0f-ratio))+specular*specular;
}