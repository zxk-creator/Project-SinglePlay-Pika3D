using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 体力瓜分圆环组件（挂到任意 UI 物体上即可，编辑模式下即可看到效果）。
/// 圆环先被负面条（睡眠/中毒/饥饿/生命损失/排泄）各占一段，
/// 剩下的部分就是真体力条区域；真体力条内部再用 healthRatio 分成两段：
///   - 未消耗：不透明白色（healthRatio 越大白色越多）
///   - 已消耗：半透明白色（其余部分）
/// 负面条越高白色区域越短；真体力消耗只缩小白色的不透明段，负面条不动。
/// 每段独立颜色，中间天然镂空。手动改字段立即生效（编辑模式可见），
/// SetRatios / Set*Ratio 写同一组字段并触发重绘，以最后一次设置为准。
/// </summary>
[ExecuteAlways]
public class StaminaRing : MaskableGraphic
{
    [Header("环带样式")]
    [Range(0.05f, 0.9f)]
    public float thickness = 0.2f;
    public float startAngle = -90f;
    public float gapAngle = 1f;

    [Header("随机噪点（对每段半径做确定性扰动，编辑模式稳定）")]
    public bool enableNoise = false;
    [Range(0f, 0.3f)]
    public float noiseAmount = 0.02f;
    [Range(1f, 60f)]
    public float noiseFrequency = 20f;
    public float noiseSeed = 0f;

    [Header("内部斑点（黑色小块，确定性随机分布）")]
    public bool enableSpots = false;
    [Range(0, 40)]
    public int spotCount = 8;
    [Range(0.01f, 0.3f)]
    public float spotRadius = 0.06f;
    public Color spotColor = new Color(0f, 0f, 0f, 0.7f);

    [Header("真体力条未消耗比例（0-1，白色不透明段占真体力区域的比例）")]
    [Range(0f, 1f)]
    public float healthRatio = 1f;

    [Header("负面条比例（0-1）")]
    [Range(0f, 1f)]
    public float sleepRatio = 0f;
    [Range(0f, 1f)]
    public float poisonRatio = 0.1f;
    [Range(0f, 1f)]
    public float hungerRatio = 0.2f;
    [Range(0f, 1f)]
    public float lifeLossRatio = 0.1f;
    [Range(0f, 1f)]
    public float excretionRatio = 0.1f;

    [Header("瓜分段颜色（睡眠/中毒/饥饿/生命损失/排泄/真体力/已消耗）")]
    public Color sleepColor = new Color(0.5f, 0.6f, 1f);
    public Color poisonColor = new Color(0.8f, 0.3f, 1f);
    public Color hungerColor = new Color(1f, 0.8f, 0.2f);
    public Color lifeLossColor = new Color(1f, 0.3f, 0.3f);
    public Color excretionColor = new Color(0.7f, 0.5f, 0.3f);
    public Color healthColor = Color.white;
    public Color consumedHealthColor = new Color(1f, 1f, 1f, 0.4f);

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        SetVerticesDirty();
    }
    #endif

    /// <summary>一次性设置全部比例（0-1）：真体力未消耗比例 + 5个负面比例</summary>
    public void SetRatios((float health, float sleep, float poison, float hunger, float lifeLoss, float excretion) value)
    {
        healthRatio = Mathf.Clamp01(value.health);
        sleepRatio = Mathf.Clamp01(value.sleep);
        poisonRatio = Mathf.Clamp01(value.poison);
        hungerRatio = Mathf.Clamp01(value.hunger);
        lifeLossRatio = Mathf.Clamp01(value.lifeLoss);
        excretionRatio = Mathf.Clamp01(value.excretion);
        SetVerticesDirty();
    }

    public void SetHealthRatio(float v)    { healthRatio = Mathf.Clamp01(v); SetVerticesDirty(); }
    public void SetSleepRatio(float v)     { sleepRatio = Mathf.Clamp01(v); SetVerticesDirty(); }
    public void SetPoisonRatio(float v)    { poisonRatio = Mathf.Clamp01(v); SetVerticesDirty(); }
    public void SetHungerRatio(float v)    { hungerRatio = Mathf.Clamp01(v); SetVerticesDirty(); }
    public void SetLifeLossRatio(float v)  { lifeLossRatio = Mathf.Clamp01(v); SetVerticesDirty(); }
    public void SetExcretionRatio(float v) { excretionRatio = Mathf.Clamp01(v); SetVerticesDirty(); }

    // 真体力条区域 = 负面条占完后剩下的部分（0-1）
    public float HealthRegionRatio
    {
        get
        {
            float occupied = sleepRatio + poisonRatio + hungerRatio + lifeLossRatio + excretionRatio;
            return Mathf.Clamp01(1f - occupied);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerRadius = outerRadius * (1f - Mathf.Clamp01(thickness));
        if (outerRadius <= 0f) return;

        float angle = startAngle;

        // 负面条：睡眠/中毒/饥饿/生命损失/排泄
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, sleepRatio, sleepColor);
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, poisonRatio, poisonColor);
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, hungerRatio, hungerColor);
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, lifeLossRatio, lifeLossColor);
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, excretionRatio, excretionColor);

        // 真体力条区域：未消耗（不透明白）+ 已消耗（半透明白）
        float regionRatio = HealthRegionRatio;
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, regionRatio * healthRatio, healthColor);
        DrawSegment(vh, center, outerRadius, innerRadius, ref angle, regionRatio * (1f - healthRatio), consumedHealthColor);

        // 内部斑点：分布在环带（内弧~外弧之间），确定性随机
        if (enableSpots && spotCount > 0)
        {
            float bandInner = innerRadius + (outerRadius - innerRadius) * 0.15f;
            float bandOuter = outerRadius - (outerRadius - innerRadius) * 0.15f;
            float spotRadiusPx = spotRadius * outerRadius;
            for (int i = 0; i < spotCount; i++)
            {
                DrawSpot(vh, center, bandInner, bandOuter, spotRadiusPx, i);
            }
        }
    }

    // 确定性伪随机（0-1），同一种子+索引永远得到相同值，编辑模式稳定
    private float DeterministicRandom(int index, int salt)
    {
        uint h = (uint)(index * 374761393 + salt * 668265263) ^ (uint)(noiseSeed * 1000003f);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0xFFFFFF;
    }

    // 画一个不规则小块斑点：中心随机在环带内，外圈若干顶点半径做随机扰动
    private void DrawSpot(VertexHelper vh, Vector2 center, float bandInner, float bandOuter,
                          float spotRadiusPx, int index)
    {
        float centerAngle = DeterministicRandom(index, 0) * 360f;
        float centerR = Mathf.Lerp(bandInner, bandOuter, DeterministicRandom(index, 1));

        float centerRad = centerAngle * Mathf.Deg2Rad;
        Vector2 centerPos = center + new Vector2(Mathf.Cos(centerRad), Mathf.Sin(centerRad)) * centerR;

        int vertexCount = 6 + (int)(DeterministicRandom(index, 2) * 4); // 6-9 个外圈点
        int baseVert = vh.currentVertCount;
        vh.AddVert(centerPos, spotColor, Vector2.zero);

        for (int v = 0; v < vertexCount; v++)
        {
            float t = (float)v / vertexCount;
            float ang = (centerAngle - 180f + t * 360f + (DeterministicRandom(index, 3 + v) - 0.5f) * 30f) * Mathf.Deg2Rad;
            float rr = centerR + (DeterministicRandom(index, 20 + v) - 0.5f) * 2f * spotRadiusPx;
            rr = Mathf.Clamp(rr, bandInner, bandOuter);
            Vector2 pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
            vh.AddVert(pos, spotColor, Vector2.zero);
        }

        for (int v = 0; v < vertexCount; v++)
        {
            int next = baseVert + 1 + ((v + 1) % vertexCount);
            vh.AddTriangle(baseVert, baseVert + 1 + v, next);
        }
    }

    private void DrawSegment(VertexHelper vh, Vector2 center, float outerRadius, float innerRadius,
                             ref float angle, float ratio, Color color)
    {
        float sweep = Mathf.Clamp01(ratio) * 360f;
        if (sweep > 0f)
        {
            float drawSweep = sweep - gapAngle;
            if (drawSweep > 0f)
                AddArc(vh, center, outerRadius, innerRadius, angle, drawSweep, color);
        }
        angle += sweep;
    }

    private const int STEP_DEGREE = 4;

    private void AddArc(VertexHelper vh, Vector2 center, float outerRadius, float innerRadius,
                        float startDeg, float sweepDeg, Color color)
    {
        int stepCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweepDeg) / STEP_DEGREE));
        int baseVert = vh.currentVertCount;

        for (int i = 0; i <= stepCount; i++)
        {
            float t = (float)i / stepCount;
            float deg = startDeg + sweepDeg * t;
            float rad = deg * Mathf.Deg2Rad;

            float o = outerRadius;
            float r = innerRadius;
            if (enableNoise)
            {
                // 内外弧各自采样 Perlin 噪声，得到少量随机扰动（角度域连续、确定性）
                float noiseOut = Mathf.PerlinNoise(rad * noiseFrequency, noiseSeed) - 0.5f;
                float noiseIn = Mathf.PerlinNoise(rad * noiseFrequency, noiseSeed + 100f) - 0.5f;
                o = outerRadius * (1f + noiseOut * noiseAmount);
                r = innerRadius * (1f + noiseIn * noiseAmount);
            }

            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            vh.AddVert(center + dir * o, color, Vector2.zero);
            vh.AddVert(center + dir * r, color, Vector2.zero);
        }

        for (int i = 0; i < stepCount; i++)
        {
            int v0 = baseVert + i * 2;
            int v1 = v0 + 1;
            int v2 = v0 + 2;
            int v3 = v0 + 3;
            vh.AddTriangle(v0, v2, v1);
            vh.AddTriangle(v1, v2, v3);
        }
    }
}
