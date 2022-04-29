// 草啊，为什么要写这个东西
// 希望写熟了以后比 CommandBuffer 要好用

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LTCRenderPassFeature : ScriptableRendererFeature
{

    [System.Serializable] //脚本序列化

    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRendering;

        //避免我们写的东西与URP自身的东西干扰，所以我们写的东西默认在URP的后处理之前
        //为何要做这个enum？因为有时我们并不想让透明物体也被描边。即对render queue进行判断。
        public Shader shader; //汇入shader。
    }

    public Settings settings = new Settings(); //开放设置

    //【我们主要在这里做pass ↓ ↓ ↓】
    LTCPass ltcPass;
    //【我们主要在这里做pass ↑ ↑ ↑】

    //【Create】
    public override void Create()
    {
        this.name = "LTCPass"; //你的名字
        ltcPass = new LTCPass(RenderPassEvent.AfterRendering, settings.shader);

        //ltcPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    //【AddRenderPasses】
    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData
    )
    {
        ltcPass.Setup(renderer.cameraColorTarget); //初始化
        renderer.EnqueuePass(ltcPass);

        // colorTintPass.Setup(renderer.cameraColorTarget); //初始化
        // renderer.EnqueuePass(colorTintPass); //汇入render queue
    }
}

//【pass】
public class LTCPass : ScriptableRenderPass
{
    static readonly string k_RenderTag = "LTC Lights Effects"; //在渲染队列里的名字

    static readonly int TempTargetId = Shader.PropertyToID("_TempTargetLTC");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex"); //暂存，名字是shader里的那个

    Material LTCMaterial;
    RenderTargetIdentifier currentTarget;

    //【渲染事件】
    public LTCPass(RenderPassEvent evt, Shader AreaLightShader)
    {
        renderPassEvent = evt;
        var shader = AreaLightShader;

        if (shader == null)
        {
            Debug.LogError("There is no LTC Shader!");
            return;
        }

        LTCMaterial = CoreUtils.CreateEngineMaterial(AreaLightShader); //新建材质
    }

    //【初始化】
    public void Setup(in RenderTargetIdentifier currentTarget)
    {
        this.currentTarget = currentTarget;
    }

    //【OnCameraSetup】
    //public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) { }

    //【Execute】
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (LTCMaterial == null) //如果材质不存在
        {
            Debug.LogError("There is no LTC Material!");
            return;
        }

        var cmd = CommandBufferPool.Get(k_RenderTag); //拿到profilerTag

        //【我们主要在这里做render ↓ ↓ ↓】
        Render(cmd, ref renderingData); //渲染函数
        //【我们主要在这里做render ↑ ↑ ↑】

        context.ExecuteCommandBuffer(cmd); //执行函数，回收
        CommandBufferPool.Release(cmd);
    }

    //【渲染】
    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        ref var cameraData = ref renderingData.cameraData;
        var camera = cameraData.camera;
        var source = currentTarget; //当前帧以图片汇入
        int destination = TempTargetId; //中途使用的渲染目的地

        //LTCMaterial.SetColor(0, 0, 1, 1);

        cmd.SetGlobalTexture(MainTexId, source);

        cmd.GetTemporaryRT(
            destination,
            cameraData.camera.scaledPixelWidth,
            cameraData.camera.scaledPixelHeight,
            0,
            FilterMode.Trilinear, //三线性
            RenderTextureFormat.ARGB32
        );

        //设置color tint render target
        cmd.Blit(source, destination);
        cmd.Blit(destination, source, LTCMaterial, 0); //叠一次 CalculateLightDeferred
    }

    //【OnCameraCleanup】
    public override void OnCameraCleanup(CommandBuffer cmd)
    {

    }
}
