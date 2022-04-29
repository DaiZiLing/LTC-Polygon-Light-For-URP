//===========================================================
//
//  URP LTC 光源实现
//  Unity Version 2020.3.30
//  URP Version 10.3
//  ref.
//  https://github.com/Unity-Technologies/VolumetricLighting
//
//===========================================================

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[ExecuteInEditMode]
public class LTCAreaLight : MonoBehaviour
{
    //=====================================================================================

    public bool m_RenderSource = true; //是否显示光源的平面（mesh)
    public Vector3 m_Size = new Vector3(1, 1, 1); //光源的尺寸，长宽

    [Range(0, 179)]
    public float m_Angle = 0.0f; //光源可照明的角度范围

    [Range(0, 20)]
    public float m_Intensity = 0.8f; //光源密度
    public Color m_Color = Color.white; //光源颜色

    MeshRenderer m_SourceRenderer; //给光源mesh一个render
    Mesh m_SourceMesh; //给光源一个mesh

    //[HideInInspector]
    public Mesh m_Quad;
    Vector2 m_CurrentQuadSize = Vector2.zero; //一开始，Quad的尺寸为 0*0
    Vector3 m_CurrentSize = Vector3.zero; //一开始，光锥的体积为 0*0*0
    float m_CurrentAngle = -1.0f;

    bool m_Initialized = false;

    MaterialPropertyBlock m_props;

    static Vector3[] vertices = new Vector3[4];

    //=====================================================================================

    // Dictionary<Camera, CommandBuffer> m_Cameras = new Dictionary<Camera, CommandBuffer>();
    // static CameraEvent kCameraEvent = CameraEvent.AfterLighting;
    static readonly float[,] offsets = new float[4, 2]
    {
        { 1  ,  1 },
        { 1  , -1 },
        {-1  , -1 },
        {-1  ,  1 }
    };

    public Material m_ProxyMaterial;

	//[HideInInspector]
    public Shader m_ProxyShader;

    public Texture2D s_TransformInvTexture_Specular;
    public Texture2D s_TransformInvTexture_Diffuse;
    public Texture2D s_AmpDiffAmpSpecFresnel;

	//[HideInInspector]
    public Mesh m_Cube;

    //====================================================================================

    void Awake()
    {
        if (!Init())
            return;

        UpdateSourceMesh();
        
    }

    void OnEnable()
    {
        m_props = new MaterialPropertyBlock();

        if (!Init())
            return;
        UpdateSourceMesh();
    }

    void OnDisable()
    {
        // if (!Application.isPlaying)
        // {
        //     Cleanup();
        // }
        // else
        //     for (var e = m_Cameras.GetEnumerator(); e.MoveNext(); )
        //         if (e.Current.Value != null)
        //             e.Current.Value.Clear();
    }

    void OnDestroy()
    {
        if (m_ProxyMaterial != null)
            DestroyImmediate(m_ProxyMaterial);
        if (m_SourceMesh != null)
            DestroyImmediate(m_SourceMesh);
        // Cleanup();
    }

    void UpdateSourceMesh() //【更新光源的形状】
    {
        m_Size.x = Mathf.Max(m_Size.x, 0);
        m_Size.y = Mathf.Max(m_Size.y, 0);
        m_Size.z = Mathf.Max(m_Size.z, 0);

        Vector2 quadSize =
            m_RenderSource && enabled ? new Vector2(m_Size.x, m_Size.y) : Vector2.one * 0.0001f;
        // ↑ 这种逻辑电路一样的的表达式看得头疼死了
        // 意思：quadSize 在 m_RenderSource Yes 时，等于 Vector2(m_Size.x, m_Size.y)，不然就是 Vector2.one * 0.0001f。
        // quadSize在不显示光源时，变的特别小了？

        if (quadSize != m_CurrentQuadSize)
        {
            float x = quadSize.x * 0.5f;
            float y = quadSize.y * 0.5f;
            // To prevent the source quad from getting into the shadowmap, offset it back a bit.
            // 为了避免光源不能与shadowmap很好地混合，做了一个小小的bias
            float z = -0.001f;

            // 作者的原顶点分布绕序，在高版本Unity内有问题
            // vertices[0].Set (-x,   y,  z);
            // vertices[1].Set ( x,  -y,  z);
            // vertices[2].Set ( x,   y,  z);
            // vertices[3].Set (-x,  -y,  z);

            // 2  0
            // 1  3

            // 我改了一下：
            vertices[0].Set ( x, -y, z);
            vertices[1].Set (-x, -y, z);
            vertices[2].Set ( x,  y, z);
            vertices[3].Set (-x,  y, z);

            // 0  1
            // 2  3

            m_SourceMesh.vertices = vertices; //更新顶点

            m_CurrentQuadSize = quadSize; //更新四方格大小
        }

        if (m_Size != m_CurrentSize || m_Angle != m_CurrentAngle)
        {
            // Set the bounds of the mesh to large, so that they drive rendering of the entire light
            // TODO: Make the bounds tight around the shape of the light. Right now they're just tight around
            // the shadow frustum, which is fine if the shadows are enable (ok, maybe far plane should be more clever),
            // but doesn't make sense if shadows are disabled.
            // 作者在做这个东西的时候，影子关掉后，算这个bound就没有意义了
            // 他打算以后再做改进
            m_SourceMesh.bounds = GetFrustumBounds(); 
			//如果size不等于当前size，或者angle不等于当前angle，就...
			//【拿到光锥的包围区域】
        }
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy || !enabled)
        {
            // Cleanup();
            return;
        }

        if (!Init())
            return;

        UpdateSourceMesh();

        // if (Application.isPlaying)
        //     for (var e = m_Cameras.GetEnumerator(); e.MoveNext(); )
        //         if (e.Current.Value != null)
        //             e.Current.Value.Clear();
    }

    void OnWillRenderObject()
    {
        if (!Init())
            return;

        // TODO: This is just a very rough guess. Need to properly calculate the surface emission
        // intensity based on light's intensity.
        Color color = new Color
		(
            Mathf.GammaToLinearSpace(m_Color.r),
            Mathf.GammaToLinearSpace(m_Color.g),
            Mathf.GammaToLinearSpace(m_Color.b),
            1.0f
        );

        m_props.SetVector("_EmissionColor", color * m_Intensity);
        m_SourceRenderer.SetPropertyBlock(m_props);

        //SetUpCommandBuffer();
    }

	//【从这里到330行左右，是怎么在用户界面画 Gizmo，和咱们实现 LTC 关系不大】

    public Matrix4x4 GetProjectionMatrix(bool linearZ = false)  //如果不是线性 Z
    {
        Matrix4x4 m;

        if (m_Angle == 0.0f) // Angel等于0时，光锥为一个box，是正交
        {
            m = Matrix4x4.Ortho
			(
                -0.5f * m_Size.x,
                0.5f * m_Size.x,
                -0.5f * m_Size.y,
                0.5f * m_Size.y,
                0,
                -m_Size.z
            );
        }

        else
        {
            float near = GetNearToCenter();
            if (linearZ)
            {
                m = PerspectiveLinearZ(m_Angle, m_Size.x / m_Size.y, near, near + m_Size.z);
            }
            else
            {
                m = Matrix4x4.Perspective(m_Angle, m_Size.x / m_Size.y, near, near + m_Size.z);
                m = m * Matrix4x4.Scale(new Vector3(1, 1, -1));
            }
            m = m * GetOffsetMatrix(near);
        }

        return m * transform.worldToLocalMatrix;
    }

    float GetNearToCenter() //与 X 无关
    {
        if (m_Angle == 0.0f)
            return 0;

        return m_Size.y * 0.5f / Mathf.Tan(m_Angle * 0.5f * Mathf.Deg2Rad);
    }

    Matrix4x4 GetOffsetMatrix(float zOffset)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(3, new Vector4(0, 0, zOffset, 1));
        return m;
    }

    public Vector4 MultiplyPoint(Matrix4x4 m, Vector3 v)
    {
        Vector4 res;
        res.x = m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03;
        res.y = m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13;
        res.z = m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23;
        res.w = m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33;
        return res;
    }

    public Vector4 GetPosition()
    {
        Transform t = transform;

        if (m_Angle == 0.0f)
        {
            Vector3 dir = -t.forward;
            return new Vector4(dir.x, dir.y, dir.z, 0);
        }

        Vector3 pos = t.position - GetNearToCenter() * t.forward;
        return new Vector4(pos.x, pos.y, pos.z, 1);
    }

    void OnDrawGizmosSelected()   //绘制光锥的Gizmo
    {
        Gizmos.color = Color.white;

        if (m_Angle == 0.0f)     //如果angle为0，就绘制一个边框为白色的box
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, 0, 0.5f * m_Size.z), m_Size);
            return;
        }

        float near = GetNearToCenter();       // Near边框绘制
        Gizmos.matrix = transform.localToWorldMatrix * GetOffsetMatrix(-near);

        Gizmos.DrawFrustum(Vector3.zero, m_Angle, near + m_Size.z, near, m_Size.x / m_Size.y);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Bounds bounds = GetFrustumBounds();
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    Matrix4x4 PerspectiveLinearZ(float fov, float aspect, float near, float far)    //线性深度情况下的透视矩阵（一般用不上）
    {
        // A vector transformed with this matrix should get perspective division on x and y only:
        // Vector4 vClip = MultiplyPoint(PerspectiveLinearZ(...), vEye);
        // Vector3 vNDC = Vector3(vClip.x / vClip.w, vClip.y / vClip.w, vClip.z);
        // vNDC is [-1, 1]^3 and z is linear, i.e. z = 0 is half way between near and far in world space.

        float rad = Mathf.Deg2Rad * fov * 0.5f;
        float cotan = Mathf.Cos(rad) / Mathf.Sin(rad);
        float deltainv = 1.0f / (far - near);
        Matrix4x4 m;

        m.m00 = cotan / aspect;
        m.m01 = 0.0f;
        m.m02 = 0.0f;
        m.m03 = 0.0f;
        m.m10 = 0.0f;
        m.m11 = cotan;
        m.m12 = 0.0f;
        m.m13 = 0.0f;
        m.m20 = 0.0f;
        m.m21 = 0.0f;
        m.m22 = 2.0f * deltainv;
        m.m23 = -(far + near) * deltainv;
        m.m30 = 0.0f;
        m.m31 = 0.0f;
        m.m32 = 1.0f;
        m.m33 = 0.0f;

		//  | cotan / aspect     0               0             0 |
		//  |      0           cotan             0             0 |
		//  |      0             0        2*deltainv   -(far + near) * deltainv|
		//  |      0             0               1             0 |

        return m;
    }

    Bounds GetFrustumBounds()
    {
        if (m_Angle == 0.0f)
            return new Bounds(Vector3.zero, m_Size); //如果光锥角为0，不管了，直接为0

        //摄影机那一套，求TBRLNF之类的
        float tanhalffov = Mathf.Tan(m_Angle * 0.5f * Mathf.Deg2Rad); //一半FOV角度的tan
        float near = m_Size.y * 0.5f / tanhalffov;
        float z = m_Size.z;
        float y = (near + m_Size.z) * tanhalffov * 2.0f;
        float x = m_Size.x * y / m_Size.y;
        return new Bounds(Vector3.forward * m_Size.z * 0.5f, new Vector3(x, y, z));
        //好，拿到了光锥透视包围盒
    }

    bool Init()
    {
        if (m_Initialized)
            return true;
        //把脚本打上勾

		if (m_Quad == null || !InitDirect())
			return false;

        m_SourceRenderer = GetComponent<MeshRenderer>(); //调光源的render
        m_SourceRenderer.enabled = true; //显示
        m_SourceMesh = Instantiate<Mesh>(m_Quad); //是个box
        m_SourceMesh.hideFlags = HideFlags.HideAndDontSave; //不在Hierarchy,是工具人

        MeshFilter mfs = gameObject.GetComponent<MeshFilter>();
        mfs.sharedMesh = m_SourceMesh;   //  Mesh (clone)

        Transform t = transform;

        if (t.localScale != Vector3.one)
        {
#if UNITY_EDITOR
            Debug.LogError("AreaLights don't like to be scaled. Setting local scale to 1.", this);
#endif
            t.localScale = Vector3.one;
        } //光源quad的比例和脚本里一致。

        SetUpLUTs();

        m_Initialized = true;
        return true;
    }

    // void Cleanup() //清理
    // {
    //     for (var e = m_Cameras.GetEnumerator(); e.MoveNext(); )
    //     {
    //         var cam = e.Current;
    //         if (cam.Key != null && cam.Value != null)
    //         {
    //             cam.Key.RemoveCommandBuffer(kCameraEvent, cam.Value);
    //         }
    //     }
    //     m_Cameras.Clear();
    // }

    // public void SetUpCommandBuffer()
    // {
    //     Camera cam = Camera.current; //当前摄影机
    //     CommandBuffer buf = GetOrCreateCommandBuffer(cam);    //拿到CB

    //     buf.SetGlobalVector("_LightPos", transform.position); //CB整一个位置
    //     buf.SetGlobalVector("_LightColor", GetColor()); //CB整一个颜色
    //     buf.SetGlobalFloat("_LightAsQuad", 0);

    //     SetUpLUTs();

    //     float z = 0.01f;
    //     Transform t = transform;

    //     Matrix4x4 lightVerts = new Matrix4x4();
    //     for (int i = 0; i < 4; i++)
    //     {
    //         lightVerts.SetRow(
    //             i,
    //             t.TransformPoint(
    //                 new Vector3(m_Size.x * offsets[i, 0], m_Size.y * offsets[i, 1], z) * 0.5f
    //             )
    //         );
    //     }
    //     // lightVerts四个顶点

    //     buf.SetGlobalMatrix("_LightVerts", lightVerts);

    //     Matrix4x4 m = Matrix4x4.TRS(
    //         new Vector3(0, 0, 10.0f),
    //         Quaternion.identity,
    //         Vector3.one * 20
    //     );
    //     buf.DrawMesh(m_Cube, t.localToWorldMatrix * m, m_ProxyMaterial, 0, 0);
    // }

    Color GetColor() //伽马还是线性啊？
    {
        if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
            return m_Color * m_Intensity;

        return new Color(
            Mathf.GammaToLinearSpace(m_Color.r * m_Intensity),
            Mathf.GammaToLinearSpace(m_Color.g * m_Intensity),
            Mathf.GammaToLinearSpace(m_Color.b * m_Intensity),
            1.0f
        );
    }

    // URP 用 renderfeature 代替了CommandBuffer
    // // Camera.AddCommandBuffer 在 URP 里已经不起作用了
    // CommandBuffer GetOrCreateCommandBuffer(Camera cam)
    // {
    // 	if(cam == null)
    // 	return null;

    // 	CommandBuffer buf = null;

    // 	if(!m_Cameras.ContainsKey(cam))
    // 	{
    // 		buf = new CommandBuffer();
    // 		buf.name = /*"Area light: " +*/ gameObject.name;
    // 		m_Cameras[cam] = buf;
    // 		cam.AddCommandBuffer(kCameraEvent, buf);
    // 		cam.depthTextureMode |= DepthTextureMode.Depth;
    // 	}
    // 	else
    // 	{
    // 		buf = m_Cameras[cam];
    // 		buf.Clear();
    // 	}

    // 	return buf;
    // }

    bool InitDirect()
    {
        if (m_ProxyShader == null || m_Cube == null)  //如果没拿到shader和cube，就返回false
            return false;

        // Proxy
        m_ProxyMaterial = new Material(m_ProxyShader);
        m_ProxyMaterial.hideFlags = HideFlags.HideAndDontSave;//用于纯脚本创建的工具人材质

        return true;
    }

    // void SetKeyword(string keyword, bool on)  //用于SetUpCommandBuffer画box用的判据，用不上了
    // {
    //     if (on)
    //         m_ProxyMaterial.EnableKeyword(keyword);
    //     else
    //         m_ProxyMaterial.DisableKeyword(keyword);
    // }

    // 如果不加载这个LUT，那么现有的仅仅是一个cos分布，被照亮物体的粗糙度改变，不会影响高光。
    void SetUpLUTs()
    {
        if (s_TransformInvTexture_Diffuse == null)
        {
            s_TransformInvTexture_Diffuse = AreaLightLUT.LoadLUT
			(
                AreaLightLUT.LUTType.TransformInv_DisneyDiffuse
            );
        }
            

        if (s_TransformInvTexture_Specular == null)
        {
            s_TransformInvTexture_Specular = AreaLightLUT.LoadLUT
			(
                AreaLightLUT.LUTType.TransformInv_GGX
            );
        }
            

        if (s_AmpDiffAmpSpecFresnel == null)
        {
            s_AmpDiffAmpSpecFresnel = AreaLightLUT.LoadLUT
			(
                AreaLightLUT.LUTType.AmpDiffAmpSpecFresnel
            );
        } 

        m_ProxyMaterial.SetTexture("_TransformInv_Diffuse", s_TransformInvTexture_Diffuse);
        m_ProxyMaterial.SetTexture("_TransformInv_Specular", s_TransformInvTexture_Specular);
        m_ProxyMaterial.SetTexture("_AmpDiffAmpSpecFresnel", s_AmpDiffAmpSpecFresnel);
    }
}
