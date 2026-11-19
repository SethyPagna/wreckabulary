#ifndef WRECKABULARY_BACKDROP_COMMON_INCLUDED
#define WRECKABULARY_BACKDROP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    half4 _EmissionColor;
    half _Smoothness;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float, _Smoothness)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float unity_DOTS_Sampled_Smoothness;

void SetupDOTSBackdropMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_Smoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Smoothness);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSBackdropMaterialPropertyCaches()

#define _BaseColor unity_DOTS_Sampled_BaseColor
#define _EmissionColor unity_DOTS_Sampled_EmissionColor
#define _Smoothness unity_DOTS_Sampled_Smoothness
#endif

float4 _BackdropPlayMin;
float4 _BackdropPlayMax;
float _BackdropCut;
float _BackdropWind;
half4 _BackdropCapColor;

float3 BackdropSway(float3 positionWS, half weight)
{
    float phase = dot(positionWS.xz, float2(0.29, 0.21));
    float t = _Time.y;
    float gust = 0.65 + 0.35 * sin(t * 0.41 + phase * 0.37);
    float2 offset = float2(sin(t * 1.63 + phase), 0.6 * sin(t * 1.21 + phase * 1.37 + 1.1)) * (0.075 * gust);
    return positionWS + float3(offset.x, 0.0, offset.y) * (weight * _BackdropWind);
}

float3 BackdropWorldPosition(float3 positionOS, half swayWeight)
{
    return BackdropSway(TransformObjectToWorld(positionOS), swayWeight);
}

VertexPositionInputs BackdropVertexInputs(float3 positionWS)
{
    VertexPositionInputs inputs;
    inputs.positionWS = positionWS;
    inputs.positionVS = TransformWorldToView(positionWS);
    inputs.positionCS = TransformWorldToHClip(positionWS);
    float4 ndc = inputs.positionCS * 0.5f;
    inputs.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    inputs.positionNDC.zw = inputs.positionCS.zw;
    return inputs;
}

half3 BackdropTint(half4 vertexColor)
{
    return SRGBToLinear(vertexColor.rgb);
}

bool BackdropCameraInsidePlay(float3 cameraWS)
{
    return all(cameraWS >= _BackdropPlayMin.xyz) && all(cameraWS <= _BackdropPlayMax.xyz);
}

bool BackdropHidesPlay(float3 positionWS)
{
    if (_BackdropCut < 0.5) return false;
    float3 cameraWS = GetCameraPositionWS();
    if (BackdropCameraInsidePlay(cameraWS)) return false;
    float3 direction = IsPerspectiveProjection() ? positionWS - cameraWS : GetViewForwardDir();
    float3 safe = direction + (step(0.0, direction) * 2.0 - 1.0) * 1e-7;
    float3 inverse = rcp(safe);
    float3 a = (_BackdropPlayMin.xyz - positionWS) * inverse;
    float3 b = (_BackdropPlayMax.xyz - positionWS) * inverse;
    float3 nearT = min(a, b);
    float3 farT = max(a, b);
    float enter = max(max(nearT.x, nearT.y), max(nearT.z, 0.0));
    float exit = min(min(farT.x, farT.y), farT.z);
    return enter <= exit;
}

void BackdropClip(float3 positionWS)
{
    clip(BackdropHidesPlay(positionWS) ? -1.0 : 1.0);
}

half3 BackdropCap(half3 normalWS, half3 lightDirectionWS, half3 lightColor, half3 ambient)
{
    half3 cap = _BackdropCapColor.a > 0.0 ? _BackdropCapColor.rgb : _BaseColor.rgb;
    half3 light = ambient * 0.55 + lightColor * (0.3 + 0.3 * saturate(dot(normalWS, lightDirectionWS)));
    return cap * light;
}

#endif
