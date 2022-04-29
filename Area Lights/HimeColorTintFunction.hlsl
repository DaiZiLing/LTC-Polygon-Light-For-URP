//================
struct appdata
{
    float4 vertex:POSITION;
    float2 uv:TEXCOORD0;
};
//================
struct v2f
{
    float4 vertex:SV_POSITION;
    float2 uv:TEXCOORD0;
};
//================

//sampler2D _MainTex;
TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
float4 _MainTex_ST;
float2 _MainTex_TexelSize;

v2f ColorTintVert (appdata v)
{
    v2f o;
    o.vertex = TransformObjectToHClip(v.vertex.xyz);
    o.uv = v.uv;

#if SHADER_API_D3D11
//  UNITY_UV_STARTS_AT_TOP
    if (_MainTex_TexelSize.y > 0)
    o.uv.y = 1 - o.uv.y;
#endif

// There are a few different times and ways that Unity flips the screen 
// (because DX11 likes to flip the screen and Unity tries to correct that, not because Unity is doing something dumb) 
// and I've not yet figured out when you should and shouldn't do it.

    return o;
}

//【ColorTint】正片叠底
float4 ColorTintFrag (v2f i):SV_TARGET
{
    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * 1;
    return col;
}

