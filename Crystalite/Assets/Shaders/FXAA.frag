#version 300 es
precision mediump float;

in vec2 uv;

out vec4 FragColor;

uniform sampler2D screenTexture;

uniform float width;
uniform float height;

vec2 offsets[9] = vec2[](
    vec2(-1.0f,1.0f),vec2(0.0f,1.0f),vec2(1.0f,1.0f),
    vec2(-1.0f,0.0f),vec2(0.0f,0.0f),vec2(1.0f,0.0f),
    vec2(-1.0f,-1.0f),vec2(0.0f,-1.0f),vec2(1.0f,-1.0f)
);

float kernelX[9] = float[](
    -1.0f,0.0f,1.0f,
    -2.0f,0.0f,2.0f,
    -1.0f,0.0f,1.0f
);
float kernelY[9] = float[](
    1.0f,2.0f,1.0f,
    0.0f,0.0f,0.0f,
    -1.0f,-2.0f,-1.0f
);


void main(){
    float pixelX = 1.0f/width;
    float pixelY= 1.0f/height;
    vec2 pixel = vec2(pixelX,pixelY);

    float colX = 0.0f;
    float colY = 0.0f;
    for(float j = 0.0f;j < 2.0f;j++){
        for(int i = 0;i<9;i++){
            colX += length(texture(screenTexture,uv+(offsets[i]*pixel*j)))*kernelX[i];
            colY += length(texture(screenTexture,uv+(offsets[i]*pixel*j)))*kernelY[i];
        }
    }

    vec4 smoothedOutput = texture(screenTexture,uv);
    if(abs(colX)+abs(colY) > 0.25f){

        vec2 offsetScale;
        if(abs(colX)*0.7f > abs(colY)){
            offsetScale = vec2(0.0f,1.0f);
        }
        else if(abs(colY)*0.7f > abs(colX)){
            offsetScale = vec2(1.0f,0.0f);
        }
        else{
            offsetScale = vec2(0.0f,1.0f);
        }

        offsetScale = offsetScale*(1.0f/max(float(min(colY,colX)),0.5f));
        // = vec2(clamp(colX,-1.0f,1.0f),clamp(colY,-1.0f,1.0f));
        int sampleCount = 5;
        for(float i=0.0f;i < 1.0f;i+=1.0f/float(sampleCount)){
            smoothedOutput += texture(screenTexture,uv+(pixel*offsetScale*i));
            smoothedOutput += texture(screenTexture,uv+(pixel*offsetScale*-i));
        }
        smoothedOutput /= float(sampleCount*2+1);
        smoothedOutput.a = 1.0f;

    }
    FragColor = smoothedOutput;
}