using UnityEngine;

public static class ShaderUtil
{
    /// <summary>
    /// 烘焙目标渲染器的第一个材质（sharedMaterial），生成新的 Toon 材质。
    /// </summary>
    public static Material ProcessAndCreateMaterial(Renderer targetRenderer, int outputWidth = 1024, int outputHeight = 1024)
    {
        if (targetRenderer == null) { Debug.LogError("[ShaderUtil] 目标渲染器为空"); return null; }
        return ProcessAndCreateMaterial(targetRenderer.sharedMaterial, outputWidth, outputHeight);
    }

    /// <summary>
    /// 把颜色遮罩材质烘焙成 UTS 的一张纯色贴图并生成新材质：
    /// _MainTex 与三个换色遮罩按材质参数混合 → 渲染到 RT → 读回 Texture2D → 赋给新 UTS 材质的 _MainTex。
    /// 返回 null 表示无法烘焙（调用方应保留原材质）。
    /// </summary>
    public static Material ProcessAndCreateMaterial(Material sourceMat, int outputWidth = 1024, int outputHeight = 1024)
    {
        if (sourceMat == null) { Debug.LogError("[ShaderUtil] 源材质为空"); return null; }

        // 第二套 UV 遮罩（Head2uv）无法用全屏 Blit 还原，直接放弃，保留原材质
        if (sourceMat.HasProperty("_2uvSwitch") && sourceMat.GetFloat("_2uvSwitch") != 0f)
        {
            Debug.LogWarning($"[ShaderUtil] {sourceMat.name} 使用第二套UV遮罩（_2uvSwitch=1），无法烘焙，保留原材质");
            return null;
        }

        Shader blitShader = Shader.Find("Hidden/ColorMaskBlit");
        if (blitShader == null) { Debug.LogError("[ShaderUtil] 未找到 Hidden/ColorMaskBlit Shader"); return null; }
        Material blitMat = new Material(blitShader);

        // 主纹理：优先 _MainTex；没有则退到第一张有效的遮罩贴图
        //（部分素材只有遮罩没有主图，只要有一张有效图片就能烘焙）
        Texture mainTex = sourceMat.GetTexture("_MainTex");
        if (mainTex == null)
        {
            mainTex = sourceMat.GetTexture("_ClothMask") ?? sourceMat.GetTexture("_ClothMask1") ?? sourceMat.GetTexture("_SkinMask");
            if (mainTex != null)
            {
                Debug.Log($"[ShaderUtil] {sourceMat.name} 没有 _MainTex，用遮罩贴图 {mainTex.name} 作为主纹理");
            }
        }
        if (mainTex == null)
        {
            Debug.LogError($"[ShaderUtil] {sourceMat.name} 既没有 _MainTex 也没有任何遮罩贴图，无法烘焙");
            Object.DestroyImmediate(blitMat);
            return null;
        }
        blitMat.SetTexture("_MainTex", mainTex);
        blitMat.SetVector("_MainTex_ST", sourceMat.HasProperty("_MainTex_ST") ? sourceMat.GetVector("_MainTex_ST") : new Vector4(1f, 1f, 0f, 0f));

        // 透明占位纹理：alpha=0 → step(cutout, 0)=0 → 不参与换色（缺遮罩=不换色）
        Texture2D transparentTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        transparentTex.SetPixel(0, 0, Color.clear);
        transparentTex.Apply();

        blitMat.SetTexture("_ClothMask", sourceMat.GetTexture("_ClothMask") ?? transparentTex);
        blitMat.SetTexture("_ClothMask1", sourceMat.GetTexture("_ClothMask1") ?? transparentTex);
        blitMat.SetTexture("_SkinMask", sourceMat.GetTexture("_SkinMask") ?? transparentTex);

        // 颜色参数（缺失时用中性默认值，避免把材质误烤成灰/黑）
        blitMat.SetColor("_ClothColor", sourceMat.HasProperty("_ClothColor") ? sourceMat.GetColor("_ClothColor") : Color.white);
        blitMat.SetColor("_ClothColor1", sourceMat.HasProperty("_ClothColor1") ? sourceMat.GetColor("_ClothColor1") : Color.white);
        blitMat.SetColor("_SkinColor", sourceMat.HasProperty("_SkinColor") ? sourceMat.GetColor("_SkinColor") : new Color(0.99f, 0.89f, 0.73f));
        blitMat.SetFloat("_ColorCoef", sourceMat.HasProperty("_ColorCoef") ? sourceMat.GetFloat("_ColorCoef") : 1f);
        blitMat.SetFloat("_ColorFilterCoef", sourceMat.HasProperty("_ColorFilterCoef") ? sourceMat.GetFloat("_ColorFilterCoef") : 1f);

        // 遮罩裁剪阈值
        blitMat.SetFloat("_ClothMaskCutOut", sourceMat.HasProperty("_ClothMaskCutOut") ? sourceMat.GetFloat("_ClothMaskCutOut") : 0.001f);
        blitMat.SetFloat("_ClothMaskCutOut1", sourceMat.HasProperty("_ClothMaskCutOut1") ? sourceMat.GetFloat("_ClothMaskCutOut1") : 0.001f);
        blitMat.SetFloat("_SkinMaskCutOut", sourceMat.HasProperty("_SkinMaskCutOut") ? sourceMat.GetFloat("_SkinMaskCutOut") : 0.001f);

        // 烘焙到 RenderTexture
        RenderTexture rt = RenderTexture.GetTemporary(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(null, rt, blitMat);

        Texture2D finalTex = new Texture2D(outputWidth, outputHeight, TextureFormat.ARGB32, true);
        RenderTexture.active = rt;
        finalTex.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // 整体提亮补偿（贴图色值偏暗时）：受光区与阴影区乘同一系数，
        // 明暗关系（阴影 0.7 灰）保持不变；>1 后超出部分会被 8bit 钳到白。
        const float brightness = 1.15f;
        if (brightness > 1f)
        {
            Color[] pixels = finalTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r *= brightness;
                pixels[i].g *= brightness;
                pixels[i].b *= brightness;
            }
            finalTex.SetPixels(pixels);
        }
        finalTex.Apply(true, true);

        // 创建 UTS（Unity Toon Shader）材质（fork 版：带相机距离 dither 淡出）
        Shader toonShader = Shader.Find("Toon/DitherFade");
        if (toonShader == null)
        {
            Debug.LogError("[ShaderUtil] 未找到 Toon/DitherFade Shader（Assets/Shader/UTSDither fork 未导入？）");
            Object.DestroyImmediate(finalTex);
            Object.DestroyImmediate(blitMat);
            Object.DestroyImmediate(transparentTex);
            return null;
        }
        Material newMat = new Material(toonShader);
        newMat.name = sourceMat.name + "_Toon";
        newMat.SetTexture("_MainTex", finalTex);
        newMat.SetTexture("_BaseMap", finalTex); // UTS 兼容属性，同步赋值
        // "不接受阴影"方案（模块化角色法线不可靠，绕开建模问题）：
        // - _BaseColor_Step=0：N·L 明暗全关（mask=(0−HL)/feather 恒 0）→ 角色无明暗渐变，
        //   整体恒为受光亮度，不再有"晒到太阳的面却暗淡"和动画中法线扫光导致的亮斑
        // - _Set_SystemShadowsToBase=0：系统阴影不参与角色渲染（地上影子照常，
        //   那是地面材质接收的）
        // - _GI_Intensity=0：不加环境光，角色亮度 = 贴图 × 阳光（_Is_Filter_LightColor=1
        //   截到 ≤1，阳光强度 1.5 时即贴图原色）
        // - 阴影参数（feather/阴影色）保留但不再生效，未来想恢复明暗渐变只需把
        //   _BaseColor_Step 调回 0.3~0.5
        newMat.SetFloat("_BaseColor_Step", 0f);
        newMat.SetFloat("_BaseShade_Feather", 0.07f);
        newMat.SetFloat("_1st2nd_Shades_Feather", 0.08f);
        newMat.SetColor("_1st_ShadeColor", new Color(0.7f, 0.7f, 0.7f, 1f));
        newMat.SetColor("_2nd_ShadeColor", new Color(0.7f, 0.7f, 0.7f, 1f));
        newMat.SetFloat("_Use_BaseAs1st", 1f);
        newMat.SetFloat("_Use_1stAs2nd", 1f);
        newMat.SetFloat("_GI_Intensity", 0f);
        newMat.SetFloat("_Set_SystemShadowsToBase", 0f);
        // 描边（二次元感的灵魂）：对齐 sample UnityChan（ToonShader_Main.mat）——
        // _OUTLINE_NML 关键字（顶点法线外扩）+ _Outline_Width=2.9 + 深灰描边色。
        // shader 默认值已同步为 2.9 / (0.19,0.19,0.19)，这里只需补关键字
        // （关键字无法通过 shader 默认值下发，必须逐材质 Enable）。
        newMat.EnableKeyword("_OUTLINE_NML");
        newMat.SetFloat("_Outline_Width", 2.9f);
        newMat.SetColor("_Outline_Color", new Color(0.19f, 0.19f, 0.19f, 1f));
        // 相机距离淡出（DitherFade）范围：相机距物体中心 ≤ _FadeMinDistance 完全透明，
        // ≥ _FadeMaxDistance 完全不透明，中间 4x4 Bayer 点阵渐变。
        // 覆盖 shader 默认值（0.7 / 1.5），让淡出更晚发生、过渡更柔和。
        newMat.SetFloat("_FadeMinDistance", 0.7f);
        newMat.SetFloat("_FadeMaxDistance", 1.2f);
        // 其余参数全部保持 UTS 默认：无高光（_HighColor=黑）、无 Rim（_RimLight=0）、
        // 无 MatCap（_MatCap=0）→ 无金属感、无反光。

        Object.DestroyImmediate(blitMat);
        Object.DestroyImmediate(transparentTex);

        return newMat;
    }
}
