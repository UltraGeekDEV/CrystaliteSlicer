#version 300 es
precision mediump float;

in vec3 Normal;
in vec4 Col;
in vec4 fragPosLight;
out vec4 FragColor;
uniform sampler2D shadowMap;
uniform vec3 SunPos;

void main(){
    vec4 skyCol = vec4(0.5f,0.6f,0.9f,1.0f);
    float colorDominance = 0.0f;
    vec4 shadowMixRatio = (Col*colorDominance+vec4(1.0f-colorDominance))*(Col.r*0.299f+Col.g*0.587f+Col.b*0.114f);
    float ratio;

    float shadow = 0.0f;
    vec3 lightCoords = fragPosLight.xyz / fragPosLight.w;

    float sunDot = dot(Normal,normalize(SunPos));
    float specular = max(sunDot*sunDot-0.3f,0.0f)*1.5f;

    if(lightCoords.z <= 1.0f){
        lightCoords = (lightCoords+1.0f)/2.0f;

        float currentDepth = lightCoords.z;
        
        float bias;
        if(sunDot > -0.1f){
            bias = max(0.006f*(1.0f-sunDot),0.001f);
        }
        else{
            bias = -0.001f;
        }
        
        int sampleRadius = 4;
        vec2 pixelSize = 0.5f/vec2(2048,2048);
        int sampleCount = 0;
        for(int y = -sampleRadius;y <= sampleRadius;y++){
            for(int x = -sampleRadius;x <= sampleRadius;x++){
                float closestDepth = texture(shadowMap,lightCoords.xy+vec2(x,y)*pixelSize).r;

                if(currentDepth > closestDepth + bias){
                    shadow += 1.0f;
                }
                sampleCount++;
            }
        }
        shadow = shadow/float(sampleCount);
    }
    //float shadowSpecular = max(sunDot*sunDot-0.3f,0.0f)*1.0f;
    ratio = clamp(1.0f-shadow,0.0f,1.0f);
    ratio = min(sunDot*0.8f+0.2f,ratio);
    FragColor = Col*(ratio)+(shadowMixRatio*skyCol*(1.0f-ratio)*(sunDot*0.2f+1.0f))+vec4(specular);
    //FragColor = vec4(vec3(1.0f-shadow),1.0f);
}