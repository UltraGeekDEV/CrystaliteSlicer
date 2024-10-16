#version 300 es
precision mediump float;

in vec2 uv;

out vec4 FragColor;

uniform sampler2D screenTexture;
uniform sampler2D outlineTexture;

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
    -1.0f,-2.0f,-1.0f,
    0.0f,0.0f,0.0f,
    1.0f,2.0f,1.0f
);

void main(){
    float pixelX = 1.0f/width;
    float pixelY= 1.0f/height;
    vec2 pixel = vec2(pixelX,pixelY);

    vec4 colX = vec4(0);
    vec4 colY = vec4(0);
    for(int i = 0;i<9;i++){
        colX += texture(outlineTexture,uv+(offsets[i]*pixel))*kernelX[i];
        colY += texture(outlineTexture,uv+(offsets[i]*pixel))*kernelY[i];
    }

    if(length(colX)+length(colY) > 2.0f){
        FragColor = vec4(1.0f,1.0f,0.8f,1.0f);
    }
    else{
        FragColor = texture(screenTexture,uv);
    }
}