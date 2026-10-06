using System;
using UnityEngine;

public enum EActionType
{
    Run,
    Mining,
    CutTree,
    Jump
}

/// <summary>
/// 数据层：一条体力条区域（上限100）被"真体力条 + 各负面属性"瓜分。
/// 真体力条 = 100 - 睡眠 - 中毒 - 饥饿 - 生命亏损 - 排泄。
/// 负面属性从0起不断增长（无上限、互不影响），只挤占真体力条的空间；
/// 真体力条自动恢复到"负面条未占据的最大值"，归零即昏迷（算死亡）。
/// </summary>
public class CharacterProperty : MonoBehaviour, IDeathAndRevive
{
    // 体力条总容量
    private const float MAX_VALUE = 100f;

    // 负面属性每秒增长速度
    private const float SLEEP_GAIN_PER_SECOND = 0.2f;
    private const float HUNGER_GAIN_PER_SECOND = 0.5f;
    private const float EXCRETION_GAIN_PER_SECOND = 0.2f;

    // 真体力条每秒恢复量
    private const float STAMINA_RECOVER_PER_SECOND = 5f;
    // 调用 ConsumeStamina 后停止恢复的时长（秒），结束后才开始恢复 health
    private const float STAMINA_RECOVER_DELAY = 1.5f;

    // 各操作单次消耗的真体力
    private const float RUN_COST = 0.2f;
    private const float MINING_COST = 1f;
    private const float CUT_TREE_COST = 1f;

    // sanity（理智）：独立属性，最大100，不参与瓜分生命条
    private const float SANITY_MAX_VALUE = 100f;
    // 每帧减少的速率（每秒量）
    private const float SANITY_DECAY_PER_SECOND = 0.5f;
    // 食物/中毒/睡眠回复时按回复量折算的 sanity 恢复比例（回复越多恢复越多）
    private const float SANITY_RECOVER_RATIO = 0.5f;

    // fragrance（魅力）：独立属性，最大100，不影响任何人也不受任何人影响
    private const float ODOR_MAX_VALUE = 100f;
    // 每秒衰减量（逐渐降低）
    private const float ODOR_DECAY_PER_SECOND = 0.1f;

    private const float TICK_INTERVAL = 1f;

    public event Action propertyChanged;

    // 真体力条（玩家的生命值/体力值）
    public float health {get; private set;} = MAX_VALUE;

    // 负面属性：从 0 开始增长，无上限
    private float sleep;
    private float poison;
    private float hunger;
    private float lifeLoss;
    private float excretion;

    // sanity（理智）：独立计算，最大100
    private float sanity = SANITY_MAX_VALUE;

    // 异味
    private float odor = ODOR_MAX_VALUE;

    // 影响所有负面属性每秒增加量的比例
    private float drainRatio = 0.1f;

    private bool isDead = false;
    private CharacterBase characterRef;

    private float tickAccumulator;
    // 消耗后禁止恢复的剩余时间（秒），>0 时 health 不恢复
    private float recoverBlockRemain;

    // 当前真体力条上限 = 100 - 全部负面值（最低 0）
    public float MaxHealth
    {
        get
        {
            float occupied = sleep + poison + hunger + lifeLoss + excretion;
            return Mathf.Max(0f, MAX_VALUE - occupied);
        }
    }

    public float Sanity => sanity;

    public float Odor => odor;

    private void Awake()
    {
        characterRef = GetComponent<CharacterBase>();
        if (characterRef is NPC) return;
        LocalPlayer.RegisterDeathAndReviveEvents(this);
    }

    private void OnDestroy()
    {
        LocalPlayer.UnregisterDeathAndReviveEvents(this);
    }

    public void Update()
    {
        if (isDead || characterRef.isNPC) return;

        // 消耗后的禁止恢复倒计时（每帧递减）
        if (recoverBlockRemain > 0f)
        {
            recoverBlockRemain -= Time.deltaTime;
            if (recoverBlockRemain < 0f) recoverBlockRemain = 0f;
        }

        // 真体力条自动恢复，但不超过负面条未占据的最大值，且会连续恢复，不受Tick影响
        if (recoverBlockRemain <= 0f)
        {
            health += STAMINA_RECOVER_PER_SECOND * Time.deltaTime;
            if (health > MaxHealth) health = MaxHealth;
        }

        tickAccumulator += Time.deltaTime;
        if (tickAccumulator < TICK_INTERVAL) return;
        tickAccumulator = 0f;

        // 负面属性每秒增长（生命亏损不自动增长，只能由外部手动设置）
        sleep += SLEEP_GAIN_PER_SECOND * drainRatio;
        hunger += HUNGER_GAIN_PER_SECOND * drainRatio;
        excretion += EXCRETION_GAIN_PER_SECOND * drainRatio;

        odor = Mathf.Max(0f, odor - ODOR_DECAY_PER_SECOND);
        // sanity 每帧减少少量
        sanity = Mathf.Max(0f, sanity - SANITY_DECAY_PER_SECOND);

        if (MaxHealth <= 0f)
        {
            characterRef.Dead();
        }

        propertyChanged?.Invoke();
    }

    // 设置影响所有负面属性每秒增加量的比例
    public void SetDrainRatio(float ratio)
    {
        drainRatio = Mathf.Max(0f, ratio);
        propertyChanged?.Invoke();
    }

    // 按操作类型消耗真体力（Run 为一次性小扣，Mining/CutTree 扣得多）
    public void ConsumeStamina(EActionType type)
    {
        switch (type)
        {
            case EActionType.Run:
                health -= RUN_COST;
                break;
            case EActionType.Mining:
                health -= MINING_COST;
                break;
            case EActionType.CutTree:
                health -= CUT_TREE_COST;
                break;
            case EActionType.Jump:
                health -= 10.0f;
                break;
        }

        // 消耗后 2 秒内不恢复 health
        recoverBlockRemain = STAMINA_RECOVER_DELAY;
        propertyChanged?.Invoke();
    }

    // 减少饥饿值（回复量按比例恢复 sanity）
    public void Eat(float amount)
    {
        hunger = Mathf.Max(0f, hunger - amount);
        RecoverSanity(amount);
        propertyChanged?.Invoke();
    }

    // 减少中毒值（回复量按比例恢复 sanity）
    public void Cure(float amount)
    {
        poison = Mathf.Max(0f, poison - amount);
        RecoverSanity(amount);
        propertyChanged?.Invoke();
    }

    // 减少睡眠值（回复量按比例恢复 sanity）
    public void Rest(float amount)
    {
        sleep = Mathf.Max(0f, sleep - amount);
        RecoverSanity(amount);
        propertyChanged?.Invoke();
    }

    // 减少排泄值（排泄后清零）
    public void Excrete()
    {
        excretion = 0f;
        propertyChanged?.Invoke();
    }

    // 设置生命亏损（外部传入扣除的生命值；正=增加亏损，负=恢复）
    public void AddLifeLoss(float amount)
    {
        lifeLoss = Mathf.Max(0f, lifeLoss + amount);
        propertyChanged?.Invoke();
    }

    public void AddSanity(float value)
    {
        sanity += Mathf.Clamp(value, 0f, SANITY_MAX_VALUE);
        propertyChanged?.Invoke();
    }

    public void SetOdor(float value)
    {
        odor = Mathf.Clamp(value, 0f, ODOR_MAX_VALUE);
        propertyChanged?.Invoke();
    }

    // 食物/中毒/睡眠回复时，按回复量恢复 sanity（回复越多恢复越多）
    private void RecoverSanity(float amount)
    {
        sanity = Mathf.Min(SANITY_MAX_VALUE, sanity + amount * SANITY_RECOVER_RATIO);
    }

    // 通用 Getter：各属性在 100% 区域内占有的比例（0-1 之间的小数）
    public (float health, float sleep, float poison, float hunger, float lifeLoss, float excretion) GetRatios()
    {
        return (
            Mathf.Clamp01(health / MAX_VALUE),
            Mathf.Clamp01(sleep / MAX_VALUE),
            Mathf.Clamp01(poison / MAX_VALUE),
            Mathf.Clamp01(hunger / MAX_VALUE),
            Mathf.Clamp01(lifeLoss / MAX_VALUE),
            Mathf.Clamp01(excretion / MAX_VALUE)
        );
    }

    public void OnPlayerDeath()
    {
        isDead = true;
        propertyChanged?.Invoke();
    }

    public void OnPlayerRevive()
    {
        isDead = false;
        health = MAX_VALUE;
        sleep = 0f;
        poison = 0f;
        hunger = 0f;
        lifeLoss = 0f;
        excretion = 0f;
        sanity = SANITY_MAX_VALUE;
        odor = ODOR_MAX_VALUE;
        tickAccumulator = 0f;
        recoverBlockRemain = 0f;
        propertyChanged?.Invoke();
    }
}
