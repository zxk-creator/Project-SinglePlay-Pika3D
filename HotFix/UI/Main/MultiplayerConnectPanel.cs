using UnityEngine;
using UnityEngine.UIElements;
using PK;
using PKSv;

public class MultiplayerConnectPanel : UIBase
{
    private TextField ipField;
    private TextField portField;
    private Button okButton;
    private Button cancelButton;

    // 模式：true = 主持（只填端口），false = 连接房间（IP + 端口）
    private Button hostModeButton;
    private Button joinModeButton;
    private VisualElement ipRow;
    private Label tipText;

    private bool isHost = true;

    // ===== 入场动画参数：想调节奏就改这几个 =====
    private const float Duration = 0.42f;      // 总时长（秒）
    private const float CardFromScale = 0.90f; // 卡片起始缩放
    private const float CardRiseY = 34f;       // 卡片起始下移量（px）
    private const float CardDelay = 0.06f;     // 卡片比遮罩晚多久开始（0~1，占总时长比例）
    private const float ScrimAlpha = 0.7f;     // 遮罩最终 opacity（和 USS 里 .scrim 的 0.7 相乘）

    private VisualElement scrim;
    private VisualElement card;

    private bool animating;
    private float animTime;
    private System.Action tick;

    public SaveData sv;

    public MultiplayerConnectPanel(SaveData sv) : base("MultiPlayerConnect", isUTK: true)
    {
        var root = document.rootVisualElement;

        this.sv = sv;

        ipField = root.Q<TextField>("input-ip");
        portField = root.Q<TextField>("input-port");
        okButton = root.Q<Button>("btn-ok");
        cancelButton = root.Q<Button>("btn-cancel");

        hostModeButton = root.Q<Button>("btn-mode-host");
        joinModeButton = root.Q<Button>("btn-mode-join");
        ipRow = root.Q<VisualElement>("field-ip");
        tipText = root.Q<Label>("tip-text");

        scrim = root.Q<VisualElement>("scrim");
        card = root.Q<VisualElement>("card");

        ipField.SetValueWithoutNotify("127.0.0.1");
        portField.SetValueWithoutNotify("7777");

        okButton.clicked += OnOkClicked;
        cancelButton.clicked += OnCancelClicked;

        hostModeButton.clicked += () => SetMode(true);
        joinModeButton.clicked += () => SetMode(false);

        ApplyMode();
    }

    // 切换主持 / 连接。主持时 IP 行整行隐藏，只留端口
    private void SetMode(bool wantHost)
    {
        if (isHost == wantHost) return;

        isHost = wantHost;
        ApplyMode();
    }

    private void ApplyMode()
    {
        hostModeButton.EnableInClassList("mode-button--on", isHost);
        joinModeButton.EnableInClassList("mode-button--on", !isHost);

        ipRow.style.display = isHost ? DisplayStyle.None : DisplayStyle.Flex;

        tipText.text = isHost
            ? "提示：留空则使用默认端口 7777"
            : "提示：留空则使用默认的 127.0.0.1:7777";
    }

    // ==================================================================
    // 入场动画：遮罩 0 → 0.7，卡片从透明 + 缩小 + 下移 → 完全显示
    // ==================================================================
    public override void Show()
    {
        base.Show();
        PlayEnter();
    }

    private void PlayEnter()
    {
        StopTick();

        animating = true;
        animTime = 0f;
        ApplyEnter(0f);

        tick = Tick;
        Context.updateProxy.RegisterNewTask(tick, Duration + 0.1f);
    }

    private void Tick()
    {
        if (!animating) return;

        animTime += Time.unscaledDeltaTime;
        ApplyEnter(Mathf.Clamp01(animTime / Duration));

        if (animTime >= Duration)
        {
            ApplyEnter(1f);
            ClearInline();
            StopTick();
        }
    }

    private void StopTick()
    {
        animating = false;

        if (tick != null)
        {
            Context.updateProxy.DestoryTask(tick);
            tick = null;
        }
    }

    private void ApplyEnter(float p)
    {
        if (scrim != null)
        {
            // 遮罩先走完：0 → ScrimAlpha
            scrim.style.opacity = Mathf.Lerp(0f, ScrimAlpha, Ease(0f, 0.7f, p));
        }

        if (card != null)
        {
            float k = Ease(CardDelay, 1f, p);

            card.style.opacity = k;
            card.style.translate = new Translate(0f, Mathf.Lerp(CardRiseY, 0f, k));

            float scale = Mathf.Lerp(CardFromScale, 1f, k);
            card.style.scale = new Scale(new Vector2(scale, scale));
        }
    }

    // 动画结束必须清掉内联样式，否则会和 USS 的 transition 打架
    private void ClearInline()
    {
        if (scrim != null) scrim.style.opacity = StyleKeyword.Null;

        if (card != null)
        {
            card.style.opacity = StyleKeyword.Null;
            card.style.translate = StyleKeyword.Null;
            card.style.scale = StyleKeyword.Null;
        }
    }

    private static float Ease(float from, float to, float p)
    {
        if (to <= from) return p >= to ? 1f : 0f;

        float x = Mathf.Clamp01((p - from) / (to - from));
        return 1f - (1f - x) * (1f - x);
    }

    private void OnOkClicked()
    {
        string ip = ipField.value;
        string port = portField.value;

        if (!int.TryParse(port, out int res))
        {
            new PromptMessage("输入的端口号不合法，只能输入纯数字！").Show();
            return;
        }

        if (isHost)
        {
            // 主持：只用端口
            Context.net.StartHost(res,sv);
            // 然后加入自己的游戏，使用本地连接
            Context.net.StartClient(res, "127.0.0.1", sv);
        }
        else
        {
            // 连接房间：IP + 端口
            Context.net.StartClient(res, ip,sv);
        }
    }

    private void OnCancelClicked()
    {
        StopTick();
        Hide();
    }

    public override void Destroy()
    {
        StopTick();
        base.Destroy();
    }
}
