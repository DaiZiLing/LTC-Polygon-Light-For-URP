//================
//================
//================

//============【应用阶段】================
//////////////////////////////////////////

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

//============【需要用的变量】================
/////////////////////////////////////////////

//User Control Parameters
sampler2D _HimeGoboTex;
float4    _HimeGoboTex_ST;

float4 _DiffColor;
float4 _SpecColor;

float _Roughness;
float _LightIntensity;

float _LightWidth;
float _LightHeight;

float _RotationX;
float _RotationY;
float _RotationZ;
//User Control End

//Hided Parameters
sampler2D ltc_1;
sampler2D ltc_2;

float LUT_SIZE;
float LUT_SCALE;
float LUT_BIAS;

int   NUM_SAMPLES = 1;
float pi = 3.14159265;
float NO_HIT = 1e9;
//Hided Parameters End

//============【顶点、片元】=================
/////////////////////////////////////////////
v2f LTCVert (appdata v)
{
    v2f o;
    o.vertex = TransformObjectToHClip(v.vertex.xyz);
    o.uv = v.uv * _HimeGoboTex_ST.xy + _HimeGoboTex_ST.zw;
    return o;
}

float4 LTCFrag (v2f i):SV_TARGET
{
    return float4(1, 1, 1, 1);
}

//============【下面是定制函数】===============
//////////////////////////////////////////////
