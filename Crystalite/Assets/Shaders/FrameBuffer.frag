#version 300 es
precision mediump float;

in vec2 uv;

out vec4 FragColor;

uniform sampler2D screenTexture;
uniform sampler2D outlineTexture;
uniform sampler2D normalTexture;

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

vec4 blurredGooch[9];
vec4 blurredNorm[9];

const int blurRadius = 1;

void main(){
    float pixelX = 1.0f/width;
    float pixelY= 1.0f/height;
    vec2 pixel = vec2(pixelX,pixelY);

    vec4 colX = vec4(0);
    vec4 colY = vec4(0);

    vec4 goochX = vec4(0);
    vec4 goochY = vec4(0);

    vec4 goochCol = vec4(0.6f,0.6f,0.55f,1.0f);

    float blurWeight = 1.0f / pow(float(2*blurRadius + 1),2.0f);

    for(int x = 0;x<9;x++){
        vec4 col = vec4(0);
        vec4 norm = vec4(0);
        for(int i = -blurRadius;i<=blurRadius;i++){
            for(int j = -blurRadius; j<=blurRadius;j++){
                col += texture(outlineTexture,uv+((offsets[x]+vec2(i,j))*pixel)) * blurWeight;
                norm += texture(normalTexture,uv+((offsets[x]+vec2(i,j))*pixel)) * blurWeight;
            }
        }
        blurredGooch[x] = col;
        blurredNorm[x] = norm;
    }

    for(int i = 0;i<9;i++){
        colX += blurredGooch[i]*kernelX[i];
        colY += blurredGooch[i]*kernelY[i];
        goochX += blurredNorm[i]*kernelX[i];
        goochY += blurredNorm[i]*kernelY[i];
    }

    if(length(colX)+length(colY) > 4.0f){
        FragColor = vec4(1.0f,1.0f,0.8f,1.0f);
    }
    else if(abs(goochX.w)+abs(goochY.w) > 0.8f || length(goochX.xyz) + length(goochY.xyz) > 1.0f){
        FragColor = goochCol;
    }
    else{
        FragColor = texture(screenTexture,uv);
    }
}