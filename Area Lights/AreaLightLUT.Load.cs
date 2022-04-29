// Class 包含了 AreaLightLUT.Load.cs
//              AreaLightLUT.DisneyDiffuse.cs
// 			    AreaLightLUT.GGX.cs

using UnityEngine;

public partial class AreaLightLUT
{
    const int kLUTResolution = 64;
    const int kLUTMatrixDim = 3;

    // 枚举LUT的类型
    // 被面光源照亮的物体具有diff项、spec项、fresnel项
	// LUT_1 存 Diffuse 和 GGX
	// LUT_2 存 Diffuse 和 GGX 的幅度，与一个 F 项

	// 关于LUT Trick有点迷这里，到底是一张 LUT，还是两张 LUT？
	// 我怀疑是第一张存的BRDF（粗糙度），第二张存的F（入射角角度）

    public enum LUTType
    {
        TransformInv_DisneyDiffuse,
        TransformInv_GGX,
        AmpDiffAmpSpecFresnel
    }

    public static Texture2D LoadLUT(LUTType type)
    {
        switch (type)
        {
            case LUTType.TransformInv_DisneyDiffuse: return LoadLUT(s_LUTTransformInv_DisneyDiffuse);
            case LUTType.TransformInv_GGX:           return LoadLUT(s_LUTTransformInv_GGX);
            case LUTType.AmpDiffAmpSpecFresnel:      return LoadLUT(s_LUTAmplitude_DisneyDiffuse, s_LUTAmplitude_GGX, s_LUTFresnel_GGX);
        }

        return null;
    }

    // 【两张LUT】(s_LUTTransformInv_DisneyDiffuse)  (s_LUTTransformInv_GGX);
    static Texture2D LoadLUT(double[,] LUTTransformInv)
    {
        const int count = kLUTResolution * kLUTResolution; // pixels数量
        Color[]  pixels = new Color[count];

        // transformInv
        for (int i = 0; i < count; i++)
        {
            // Only columns 0, 2, 4 and 6 contain interesting values (at least in the case of GGX).
            pixels[i] = new Color
			(
                (float)LUTTransformInv[i, 0],
                (float)LUTTransformInv[i, 2],
                (float)LUTTransformInv[i, 4],
                (float)LUTTransformInv[i, 6]
            ); // R G B
        }

        return CreateLUT(TextureFormat.RGBAHalf, pixels);
    }

    static Texture2D LoadLUT(float[] LUTScalar0, float[] LUTScalar1, float[] LUTScalar2)
	//(s_LUTAmplitude_DisneyDiffuse, s_LUTAmplitude_GGX, s_LUTFresnel_GGX)
    {
        const int count = kLUTResolution * kLUTResolution; // pixels数量
        Color[] pixels  = new Color[count];

        // amplitude
        for (int i = 0; i < count; i++)
        {
            pixels[i] = new Color(LUTScalar0[i], LUTScalar1[i], LUTScalar2[i], 0); // R G B
        }

        return CreateLUT(TextureFormat.RGBAHalf, pixels);
    }

    // 【创建LUT的函数】
	static Texture2D CreateLUT(TextureFormat format, Color[] pixels)
    {
        Texture2D tex = new Texture2D
		(
            kLUTResolution,
            kLUTResolution,
            format,
            false /*mipmap*/,
            true /*linear*/
        );
        tex.hideFlags = HideFlags.HideAndDontSave; //隐藏这个图
        tex.wrapMode  = TextureWrapMode.Clamp;  // WarpMode 为 Clamp（边界不会突变）
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
