using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PK;

/// <summary>
/// 现在这个仅作为一个傀儡作用，仅用于显示
/// </summary>
public class GameMainPanel : UIBase, IInputAcceptable
{
    private struct PropertyRing
    {
        public Image ring;
        public Func<float> getter;
        public float threshold;
        public float displayValue; // 平滑插值后的显示值
    }

    private static readonly Color normalColor = Color.white;
    private static readonly Color warningColor = new Color(1f, 0.2f, 0.2f, 1f);
    private readonly List<PropertyRing> rings = new List<PropertyRing>();

    // 圆环各段比例：目标值（来自 CharacterProperty）与平滑显示值
    private readonly float[] targetRatios = new float[6];
    private readonly float[] displayRatios = new float[6];
    private bool ratiosInitialized = false;

    // 平滑速度：每秒最多变化的比例（0-1 范围）
    public float smoothSpeed = 0.9f;

    private GameAcceptButton interactButton;
    private TMP_Text itemName;
    private TMP_Text description;
    private TMP_Text buttonOperateText;
    private RectTransform downRootTrasnfrom;

    private GameAcceptButton bagButton;
    private GameAcceptButton decorateButton;
    private GameObject sceneObjInfoRoot;
    private Image itemIcon;
    // 主体力条
    private StaminaRing mainEnergy;
    private EInteractableType currentInteractType;
    private Actor whoIsInteracting;

    public GameMainPanel() : base("GameMainPanel")
    {
        bagButton = GetTargetComponent<GameAcceptButton>("BagIcon");
        decorateButton = GetTargetComponent<GameAcceptButton>("decoration");
        description = GetTargetComponent<TMP_Text>("Description");
        buttonOperateText = GetTargetComponent<TMP_Text>("ButtonSelectText");
        itemName = GetTargetComponent<TMP_Text>("ItemName");
        interactButton = GetTargetComponent<GameAcceptButton>("AddCubButton");
        itemIcon = GetTargetComponent<Image>("ItemIcon");
        var downRoot = FindChildRecursive(UIPrefab.transform,"DownRoot");
        downRootTrasnfrom = downRoot.gameObject.GetComponent<RectTransform>();
        
        mainEnergy = GetTargetComponent<StaminaRing>();
        sceneObjInfoRoot = FindChildRecursive(UIPrefab.transform,"SceneObjectInfo").gameObject;
        sceneObjInfoRoot.SetActive(false);

        interactButton.onClick.AddListener(OnInteractButtonClick);
        bagButton.onClick.AddListener(() => { Context.localPlayer.OpenBag(); });
        decorateButton.onClick.AddListener(() =>
        {
            new PlaceFurniturePanel().Show(); 
        });

        BuildRing("Sanity", () => Context.localPlayer.property.Sanity, 20);
        BuildRing("Fragment", () => Context.localPlayer.property.Odor, 20);

        // 每帧平滑刷新
        Context.updateProxy.RegisterNewTask(SmoothRefresh, int.MaxValue, 0);
    }

    public override void Show()
    {
        UIPrefab.SetActive(true);
    }

    public override void Hide()
    {
        UIPrefab.SetActive(false);
    }

    public override void Destroy()
    {
        Context.updateProxy.DestoryTask(SmoothRefresh);
        rings.Clear();
        mainEnergy = null;
        base.Destroy();
    }

    // 这个调用点，只有玩家能够调用，所以我默认传入的是玩家。
    public void OnInteractButtonClick()
    {
        switch (currentInteractType)
        {
            case EInteractableType.Sittable:
            case EInteractableType.Toilet:
            case EInteractableType.Bed:
            case EInteractableType.Shower:
            case EInteractableType.Tub:
            case EInteractableType.Equippable:
                {
                    if (whoIsInteracting == null)
                    {
                        Log.Error("currentInteractType为null！无法交互");
                        // 隐藏
                        ShowSceneObjectInfo(("", "", "", EInteractableType.None, null));
                        return;
                    }
                    whoIsInteracting.StartInteract(Context.localPlayer.transform.position, Context.localPlayer);
                    break;
                }
            default:
                {
                    new PromptMessage("此家具不支持交互！").Show();
                    break;
                }
        }

        // 清除当前选中的按钮，使其失去焦点
        EventSystem.current.SetSelectedGameObject(null);
    }
    
    /// <summary>
    /// name为空，则不会显示。若没有检测到任何东西，传入空name即可。
    /// </summary>
    /// <param name="info"></param>
    public void ShowSceneObjectInfo((string name, string description, string iconPath, EInteractableType interactableType,Actor selfRef) info)
    {
        if (string.IsNullOrEmpty(info.name) || info.interactableType == EInteractableType.Player)
        {
            sceneObjInfoRoot.SetActive(false);
            return;
        }

        whoIsInteracting = info.selfRef;

        sceneObjInfoRoot.SetActive(true);

        itemName.text = info.name;
        description.text = info.description;
        itemIcon.sprite = Resources.Load<Sprite>(info.iconPath);
        interactButton.gameObject.SetActive(true);   // 先恢复按钮，避免上次 default 分支隐藏后回不来
        string buttonText = null;
        currentInteractType = info.interactableType;

        switch (info.interactableType)
        {
            case EInteractableType.NPC:
                {
                    buttonText = "交谈";
                    break;
                }
            case EInteractableType.Sittable:
                {
                    buttonText = "坐下";
                    break;
                }
            case EInteractableType.Toilet:
                {
                    buttonText = "上厕所";
                    break;
                }
            case EInteractableType.Mineable:
                {
                    buttonText = "开采";
                    break;
                }
            case EInteractableType.Choppable:
                {
                    buttonText = "砍伐";
                    break;
                }
            case EInteractableType.Equippable:
                {
                    buttonText = "换装";
                    break;
                }
            case EInteractableType.Stroageable:
                {
                    buttonText = "存储";
                    break;
                }
            case EInteractableType.Bed:
                {
                    buttonText = "躺一躺";
                    break;
                }
            case EInteractableType.Shower:
            case EInteractableType.Tub:
                {
                    buttonText = "洗一洗";
                    break;
                }
            // 其他的一律不可交互
            default:
                {
                    interactButton.gameObject.SetActive(false);
                    break;
                }
        }
        buttonOperateText.text = buttonText;
        LayoutRebuilder.ForceRebuildLayoutImmediate(downRootTrasnfrom);
        downRootTrasnfrom.sizeDelta = new Vector2(downRootTrasnfrom.sizeDelta.x, description.GetComponent<RectTransform>().sizeDelta.y);
    }

    private PropertyRing BuildRing(string nodeName, Func<float> getter, float threshold)
    {
        var ring = new PropertyRing
        {
            ring = GetTargetComponent<Image>(nodeName),
            getter = getter,
            threshold = threshold,
            displayValue = getter()
        };
        rings.Add(ring);
        return ring;
    }

    // 每帧：把显示值向 CharacterProperty 的真实值平滑逼近
    private void SmoothRefresh()
    {
        if (mainEnergy == null || canvasGO == null || Context.localPlayer == null)
        {
            Context.updateProxy.DestoryTask(SmoothRefresh);
            return;
        }

        CharacterProperty p = Context.localPlayer.property;

        // 平滑 Sanity / Odor 横条
        for (int i = 0; i < rings.Count; i++)
        {
            var r = rings[i];
            if (r.ring == null) continue;
            float target = r.getter();
            r.displayValue = Mathf.MoveTowards(r.displayValue, target, smoothSpeed * Time.deltaTime);
            rings[i] = r; // 结构体需写回
            r.ring.fillAmount = r.displayValue / 100f;
            r.ring.color = r.displayValue >= r.threshold ? normalColor : warningColor;
        }

        // 平滑圆环各段比例
        var ratios = p.GetRatios();
        float healthInRegion = p.MaxHealth > 0f ? p.health / p.MaxHealth : 0f;
        targetRatios[0] = healthInRegion;
        targetRatios[1] = ratios.sleep;
        targetRatios[2] = ratios.poison;
        targetRatios[3] = ratios.hunger;
        targetRatios[4] = ratios.lifeLoss;
        targetRatios[5] = ratios.excretion;

        if (!ratiosInitialized)
        {
            // 首帧直接对齐，避免开局从 0 平滑上来
            for (int i = 0; i < 6; i++) displayRatios[i] = targetRatios[i];
            ratiosInitialized = true;
        }
        else
        {
            for (int i = 0; i < 6; i++)
                displayRatios[i] = Mathf.MoveTowards(displayRatios[i], targetRatios[i], smoothSpeed * Time.deltaTime);
        }

        mainEnergy.SetRatios((health: displayRatios[0], sleep: displayRatios[1], poison: displayRatios[2],
                              hunger: displayRatios[3], lifeLoss: displayRatios[4], excretion: displayRatios[5]));
    }

    public void OnKeypadUpPressed()
    {
        new PhonePanel().Show();
    }
}
