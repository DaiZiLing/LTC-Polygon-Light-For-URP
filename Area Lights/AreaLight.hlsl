
#if AREA_LIGHT_ENABLE_DIFFUSE
	sampler2D _TransformInv_Diffuse;
#endif
//经典切两半，一个DIFF一个SPEC

sampler2D   _TransformInv_Specular;
sampler2D   _AmpDiffAmpSpecFresnel;

float4x4    _LightVerts;
// float4      _LTC_BaseMap;     //TODO：带有纹理的光源

float IntegrateEdge(float3 v1, float3 v2)
{
	float d = dot(v1,v2);
	float theta = acos(max(-0.9999, dot(v1,v2)));       //这里的的acos可以优化掉，见下
	float theta_sintheta = theta / sin(theta);
	return theta_sintheta * (v1.x*v2.y - v1.y*v2.x);
}

// //【优化后的IntegrateEdge】
// float IntegrateEdge(float3 v1, float3 v2, float3 n)
// {
	// 	float x = dot(v1,v2);
	// 	float abs_x = abs(x);

	// 	float a = 5.42031 + (3.12829 + 0.0902326 * abs_x) * abs_x;
	// 	float b = 3.45068 + (4.18814 + abs_x) * abs_x;
	// 	float theta_sintheta = a /b;
	// 	if (x < 0.0)
	// 	{
		// 		theta_sintheta = UNITY_PI * rsqrt(1.0 - x * x) - theta_sintheta;
	// 	}

	// 	float3 u = cross(v1,v2);
	// 	return theta_sintheta * dot(u, n);
// }

// Baum's equation
// Expects non-normalized vertex positions
// 此处实际为Selfshadow里的clipQuadToHorizon函数，处理方法略麻烦
// 很多人懒得看直接抄过去用（包括我）
// https://zhuanlan.zhihu.com/p/345364404
// 这篇专栏的“面光源裁剪”对它进行了详细的描述

// 裁剪：取正Z半球上的顶点。z < 0 的区域，贡献为 0
// 函数返回裁剪后的实际多边形边数。
float PolygonRadiance(float4x3 L)
{
	// detect clipping config	
	uint config = 0;
	if (L[0].z > 0) config += 1;
	if (L[1].z > 0) config += 2;
	if (L[2].z > 0) config += 4;
	if (L[3].z > 0) config += 8;

	// 一个桌子切另一个点，还剩下5条边
	// The fifth vertex for cases when clipping cuts off one corner.
	// Due to a compiler bug, copying L into a vector array with 5 rows
	// messes something up, so we need to stick with the matrix + the L4 vertex.
	float3 L4 = L[3];

	// This switch is surprisingly fast. Tried replacing it with a lookup array of vertices.
	// Even though that replaced the switch with just some indexing and no branches, it became
	// way, way slower - mem fetch stalls?

	// Selfshadow 里用的是 if else, 这里采用case

	// clip
	uint n = 0;

	switch(config)
	{
		case 0: // clip all
		break;
		
		case 1: // V1 clip V2 V3 V4
		n = 3;
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		L[2] = -L[3].z * L[0] + L[0].z * L[3];
		break;
		
		case 2: // V2 clip V1 V3 V4
		n = 3;
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
		break;
		
		case 3: // V1 V2 clip V3 V4
		n = 4;
		L[2] = -L[2].z * L[1] + L[1].z * L[2];
		L[3] = -L[3].z * L[0] + L[0].z * L[3];
		break;
		
		case 4: // V3 clip V1 V2 V4
		n = 3;	
		L[0] = -L[3].z * L[2] + L[2].z * L[3];
		L[1] = -L[1].z * L[2] + L[2].z * L[1];				
		break;
		
		case 5: // V1 V3 clip V2 V4: impossible
		break;
		
		case 6: // V2 V3 clip V1 V4
		n = 4;
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		L[3] = -L[3].z * L[2] + L[2].z * L[3];			
		break;
		
		case 7: // V1 V2 V3 clip V4
		n = 5;
		L4 = -L[3].z * L[0] + L[0].z * L[3];
		L[3] = -L[3].z * L[2] + L[2].z * L[3];
		break;
		
		case 8: // V4 clip V1 V2 V3
		n = 3;
		L[0] = -L[0].z * L[3] + L[3].z * L[0];
		L[1] = -L[2].z * L[3] + L[3].z * L[2];
		L[2] =  L[3];
		break;
		
		case 9: // V1 V4 clip V2 V3
		n = 4;
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		L[2] = -L[2].z * L[3] + L[3].z * L[2];
		break;
		
		case 10: // V2 V4 clip V1 V3: impossible
		break;
		
		case 11: // V1 V2 V4 clip V3
		n = 5;
		L[3] = -L[2].z * L[3] + L[3].z * L[2];
		L[2] = -L[2].z * L[1] + L[1].z * L[2];			
		break;
		
		case 12: // V3 V4 clip V1 V2
		n = 4;
		L[1] = -L[1].z * L[2] + L[2].z * L[1];
		L[0] = -L[0].z * L[3] + L[3].z * L[0];
		break;
		
		case 13: // V1 V3 V4 clip V2
		n = 5;
		L[3] = L[2];
		L[2] = -L[1].z * L[2] + L[2].z * L[1];
		L[1] = -L[1].z * L[0] + L[0].z * L[1];
		break;
		
		case 14: // V2 V3 V4 clip V1
		n = 5;
		L4 = -L[0].z * L[3] + L[3].z * L[0];
		L[0] = -L[0].z * L[1] + L[1].z * L[0];
		break;
		
		case 15: // V1 V2 V3 V4
		n = 4;
		break;
	}

	// normalize
	L[0] = normalize(L[0]);
	L[1] = normalize(L[1]);
	L[2] = normalize(L[2]);

	// if (n == 0)
	// 	return 0;

	// if(n == 3)
	// 	L[3] = L[0];
	// else
	// {
		// 	L[3] = normalize(L[3]);
		// 	if (n == 4)
		// 		L4 = L[0];
		// 	else
		// 		L4 = normalize(L4);
	// }

	switch (n)
	{
		case 3:
		L[3] = L[0];
		break;
		case 4:
		L[3] = normalize(L[3]);
		L4   = L[0];
		break;
		case 5:
		L[3] = normalize(L[3]);
		L4   = normalize(L4);
		break;
	}
	
	// integrate
	// 如何解决积分？

	// 0  ----  1
	// |        |
	// |        |
	// |        |
	// 3  ----  2

	// L4 是 L[3];

	float sum = 0;
	sum += IntegrateEdge(L[0], L[1]);
	sum += IntegrateEdge(L[1], L[2]);
	sum += IntegrateEdge(L[2], L[3]);

	if(n >= 4)	
	sum += IntegrateEdge(L[3], L4);
	if(n == 5)
	sum += IntegrateEdge(L4, L[0]);
	
	//sum *= 0.15915; // 1/2pi，sum *= INV_TWO_PI; // Normalization
	//作者给的系数是 0.15915，也就是除了一个 2 PI

	sum *= 0.15915;

	return max(0, sum);
}

// LUT 存的是4个参数与theta之间的关系，分别为：

//     | a 0 b |          | a 0 c |
// M = | 0 1 0 |   M^-1 = | 0 1 0 |
//     | c 0 d |          | b 0 d |

// 变换后的多边形radiance
float TransformedPolygonRadiance(float4x3 L, float2 uv, sampler2D transformInv, float amplitude)
{
	// Get the inverse LTC matrix M
	// 拿 M 的逆
	float3x3 Minv = 0;
	Minv._m22 = 1;
	Minv._m00_m02_m11_m20 = tex2D(transformInv, uv);
	// 惊了，这里居然是paper里的矩阵，↑ 我把它改成了以下的形式 ↓

	// float3x3 Minv = 0;
	// Minv._m11 = 1;
	// Minv._m00_m02_m20_m22 = tex2D(transformInv, uv);
	
	// Transform light vertices into diffuse configuration
	float4x3 LTransformed = mul(L, Minv);
	// 将上面的cos积分用 M ^ -1 变换

	// Polygon radiance in transformed configuration - specular
	return PolygonRadiance(LTransformed) * amplitude;
}

// 根据粗糙度和视角查表，组装出矩阵 M ^ -1 ;（从LUT拿到 M ,构建 M ^ -1)
// 乘上矩阵 M ^ -1 变换面光源的多边形顶点；
// 裁剪变换后的多边形至半球面空间；
// 对于后的裁剪（shading point）多边形，对每条边调用 integrateEdge() 后求和。

float3 CalculateLight (float3 position, float3 diffColor, float3 specColor, float oneMinusRoughness, float3 N, float3 lightPos, float3 lightColor)
{

	// TODO: larger and smaller values cause artifacts - why?
	// 粗糙度过小，会有 artifacts
	oneMinusRoughness = clamp(oneMinusRoughness, 0.01, 0.93);
	float roughness = 1 - oneMinusRoughness;
	float3 V = normalize(_WorldSpaceCameraPos.xyz - position.xyz);

	// Construct orthonormal basis around N, aligned with V
	// 构建关于 V 的3个正交基
	float3x3 basis;
	basis[0] = normalize(V - N * dot(V, N));               // 这是 T (w_2)
	basis[1] = normalize(cross(N, basis[0]));              // 这是 B (w_1)
	basis[2] = N;                                          // 这是 N (w_0)
	
	// Transform light vertices into that space
	// 把光的顶点转化为 D_0 （半球）那个空间
	float4x3 L;
	L = _LightVerts - float4x3(position.xyz, position.xyz, position.xyz, position.xyz);
	// 4x4 减 4x3，弹了一个 warning
	L = mul(L, transpose(basis));
	// 4x3 mul 3x3 ... 变成 4x3
	// 这几行蜜汁矩阵运算

	// L（光的顶点） 变成了 半球下，好
	// w_0 → Mw_0
	// w_1 → Mw_1
	// w_2 → Mw_2

	// UVs for sampling the LUTs
	float theta = acos(dot(V, N)); //怎么又是一个acos。。。不想优化！
	float2 uv = float2(roughness, theta/1.57);

	float3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

	// ====================================================================

	float3 result = 0;    //初始化结果

	// diff分量
	#if AREA_LIGHT_ENABLE_DIFFUSE
		float diffuseTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Diffuse, AmpDiffAmpSpecFresnel.x);
		result = diffuseTerm * diffColor;
	#endif

	// speu分量
	float specularTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Specular, AmpDiffAmpSpecFresnel.y);
	float fresnelTerm = specColor.r + (1.0 - specColor.r) * AmpDiffAmpSpecFresnel.z;
	result += specularTerm * fresnelTerm * 3.1415926;

	// 最后输出
	return result * lightColor;
}

// //============== 这一坨是StencilDeferred.shader里面的 ==================

// struct unity_v2f_deferred 
// {
// 	float4 pos : SV_POSITION;
// 	float4 uv : TEXCOORD0;
// 	float3 ray : TEXCOORD1;
// };

struct Attributes
{
	float4 positionOS : POSITION;
	uint vertexID : SV_VertexID;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float3 screenUV : TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

Varyings vert (Attributes i)
{
	Varyings o = (Varyings)0;

	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float3 positionOS = i.positionOS.xyz;

	VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS.xyz);
	o.positionCS = vertexInput.positionCS;

	o.screenUV = o.positionCS.xyw;

	return o;
}

// //==============这一坨是UnityDeferredLibrary.cginc，没用了===================

// float _LightAsQuad;

// unity_v2f_deferred vert_deferred (float4 vertex : POSITION, float3 normal : NORMAL)
// {
	//     unity_v2f_deferred o;
	//     o.pos = TransformObjectToHClip(vertex.xyz);
	//     o.uv = ComputeScreenPos(o.pos);
	//     o.ray = TransformWorldToView(TransformObjectToWorld(vertex.xyz)) * float3(-1,-1,1);
	
	//     o.ray = lerp(o.ray, normal, _LightAsQuad);

	//     return o;
// }

// //==============这一坨是UnityDeferredLibrary.cginc===================