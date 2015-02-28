﻿/*
** Used by Rendering.ImageContext
** This a partial shader used for rendering transitions between images with various effects. In order for it to work it must be assembled 
** with other files containing definitions for:

	float2 PixelTransform(in float2 texcoord);
	float4 PixelEffect(in float2 texcoord, in Texture2D InputTexture, in sampler TextureSampler, in float4 framedata) : COLOR;
	float4 Transition(in float mixAB, in float2 relativePos, in float4 colorA, in float4 colorB);
*/

Texture2D    InputTextureA   : register(t0);
SamplerState TextureSamplerA : register(s0);

Texture2D    InputTexture    : register(t1);
SamplerState TextureSampler  : register(s1);

cbuffer constants : register(b0)
{
  float4x4  worldViewProj : WORLDVIEWPROJ; // Our world view projection matrix

  // Parameters for 'new' frame B
  float4x4  g_relativetransform;
  float4    g_imagetransform;
  float4    g_framedata; // xy = width, height in pixels. z = time since rendering start in seconds. Max value 5 hours.

  // Parameters for 'old' frame A
  float4x4  g_relativetransformA;
  float4    g_imagetransformA;
  float4    g_framedataA; // xy = width, height in pixels. z = time since rendering start in seconds. Max value 5 hours.

  float4    g_borderColor;

  float     g_opacity;
  // Transition control value 0.0 = A, 1.0 = B.
  float     g_mixAB;
}

// application to vertex structure
struct VS_Input
{
  float4 clipSpaceOutput : SV_POSITION;
  float4 Position        : SCENE_POSITION;
  float2 Texcoord        : TEXCOORD0; // vertex texture coords 
};

// vertex shader to pixelshader structure
struct VS_Output
{
  float4 clipSpaceOutput  : SV_POSITION;
  float4 Position         : SCENE_POSITION;
  float2 TexcoordA        : TEXCOORD0;
  float2 TexcoordB        : TEXCOORD1;
  float2 OriginalTexcoord : TEXCOORD2;
};

// pixel shader to frame
struct PS_Output
{
  float4 Color : SV_TARGET;
};

void RenderVertexShader(in VS_Input IN, out VS_Output OUT)
{
  OUT.clipSpaceOutput = IN.clipSpaceOutput;
  OUT.Position = mul(IN.Position, worldViewProj);

  // Apply relative transform
  float2 pos = mul(float4(IN.Texcoord.x, IN.Texcoord.y, 0.0, 1.0), g_relativetransformA).xy;

  // Transform vertex coords to place brush texture
  OUT.TexcoordA = pos * g_imagetransformA.zw - g_imagetransformA.xy;

  // Apply relative transform
  pos = mul(float4(IN.Texcoord.x, IN.Texcoord.y, 0.0, 1.0), g_relativetransform).xy;

  // Transform vertex coords to place brush texture
  OUT.TexcoordB = pos * g_imagetransform.zw - g_imagetransform.xy;
  OUT.OriginalTexcoord = IN.Texcoord;
}

void RenderPixelShader(in VS_Output IN, out PS_Output OUT)
{
  float2 texcoordA = PixelTransform(IN.TexcoordA);
  float2 texcoordB = PixelTransform(IN.TexcoordB);

  float4 colorA = PixelEffect(texcoordA, InputTextureA, TextureSamplerA, g_framedataA);
  float4 colorB = PixelEffect(texcoordB, InputTexture,  TextureSampler, g_framedata);

  OUT.Color = Transition(g_mixAB, IN.OriginalTexcoord, colorA, colorB);
  OUT.Color.a *= g_opacity;
}
