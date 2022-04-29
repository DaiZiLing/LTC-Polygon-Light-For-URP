# LTC-Polygon-Light-For-URP
食用方法：一个Dummy 物体，添加 LTCAreaLight.cs，给它加一个 mesh filter，一个 mesh renderer，一个材质，应该就能用了（大概）
![image](https://github.com/DaiZiLing/LTC-Polygon-Light-For-URP/blob/main/QQ%E6%88%AA%E5%9B%BE20220429170522.jpg)

# Attention！
由于本工程的 两个 LUT 是复制 Unity 的那个，俩文件加起来有1Mb多，所以本工程就不加这个东西了
可以去这里下：https://github.com/Unity-Technologies/VolumetricLighting

【AreaLightLUT.GGX.cs】
* 【LUT 文本结构】
* s_LUTTransformInv_GGX  64 * 64 * 3 * 3
* s_LUTAmplitude_GGX  64 * 64
* s_LUTFresnel_GGX  64 * 64

【AreaLightLUT.DisneyDiffuse.cs】
* 【LUT 文本结构】
* s_LUTTransformInv_DisneyDiffuse 64 * 64 * 3 * 3
* s_LUTAmplitude_DisneyDiffuse 64 * 64
* 相比GGX，Diffuse没有菲涅尔
