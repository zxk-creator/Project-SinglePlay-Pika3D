using System.Collections.Generic;
using PKSv;
using UnityEngine;
using UnityEngine.Rendering;

public class LocalPlayer : CharacterBase, IInputAcceptable, IControlable
{ 
    ////////////////////////////////////////存档数据////////////////////////////////////////

    // 存档的数据，注意仅本地！不是NetPlayerData。为什么？因为这些东西是存在本地的！要一层网络封装没什么用！
    // 为什么new一个SaveData，作为占位符防止生成的时候为null导致NRE
    public SaveData playerSaveData { get; private set; } = new SaveData();

    ////////////////////////////////////////生命周期////////////////////////////////////////

    /// <summary>
    /// 组件引用与初始化都在 CharacterBase 里，这里只做玩家专属标记。
    /// </summary>
    protected override void Awake()
    {
        equipSystem.isNPC = false;
        base.Awake();
        // 实例化好InventorySystem，防止立即SetSaveData出现NRE。此时equip已经构造完毕
        inventory = new InventorySystem(equipSystem, this, playerSaveData);
    }

    protected override void Start()
    {
        isGirl = playerSaveData.isGirl;
        equipSystem.isGirl = isGirl;

        // 里面按顺序初始化 equipSystem 与 characterAnimation，必须先于穿衣服
        base.Start();

        // equipSystem 初始化完成后才能渲染装备
        equipReady = true;
        inventory.BindSaveData(playerSaveData);

        // 设为运动学，防止场景没生成的时候往下掉
        SetRigidbodyKinematic(true);

        // 玩家仅注册一次
        gameMainPanel = new GameMainPanel();
        gameMainPanel.Show();
        // 不销毁，除非退出到主菜单。
        Context.localPlayer = this;

        tpsCamera = GetComponent<TFPSCamera>();
        tpsCamera.TPSCameraTarget = gameObject.transform;

        Context.im.PossessNewChracter(this);
    }

    protected override void Update()
    {
        base.Update();
        if (property.health <= 0)
            StopRun();

        UpdateSynchronize();
        UpdateMeshColliderTick();
    }

    void LateUpdate()
    {
        Shader.SetGlobalVector("_UTSDitherCameraPos", tpsCamera.GetActiveCamera().transform.position);
    }

    void OnDestroy()
    {
        gameMainPanel.Hide();
    }

    ////////////////////////////////////////相机////////////////////////////////////////

    public TFPSCamera tpsCamera {get; private set;}

    public Camera GetCamera()
    {
        return tpsCamera.GetActiveCamera();
    }

    public void SwitchToCamera(bool enable)
    {
        if (enable)
        {
            tpsCamera.EnableAll();
        }
        else
        {
            tpsCamera.DisableAll();
        }
    }

    ////////////////////////////////////////旋转与移动////////////////////////////////////////

    [Header("旋转")]
    [Tooltip("角色转向平滑速度")]
    public float rotateSpeed = 10f;

    ////////////////////////////////////////背包与面板////////////////////////////////////////

    // 角色背包系统
    public InventorySystem inventory {get; private set;}

    // equipSystem.Initialize 是否已完成，未完成前不能渲染装备
    private bool equipReady;

    private GameMainPanel gameMainPanel;
    private BagPanel bagPanel;

    public void OpenBag()
    {
        bagPanel = new BagPanel(inventory);
        bagPanel.Show();
    }

    ////////////////////////////////////////位置同步////////////////////////////////////////

    private float lastSynchronizeTime = 0;

    /// <summary>每 0.1 秒发送一次位置同步信号</summary>
    private void UpdateSynchronize()
    {
        lastSynchronizeTime += Time.deltaTime;
        if (lastSynchronizeTime < 0.1) return;

        lastSynchronizeTime = 0f;

        // 发送位置同步信号
        var msg = new LocationSynchronize()
        {
            location = transform.position,
            rotation = transform.rotation,
            currentAnimation = characterAnimation.GetPlayingAnimationType()
        };

        // 1: 高频丢了无所谓的，像位置同步这样连续的
        Context.net.Send(msg, 1, LiteNetLib.DeliveryMethod.Sequenced);
    }

    ////////////////////////////////////////场景碰撞收集////////////////////////////////////////

    // 碰撞收集结果
    private List<(MeshCollider collider, Bounds bounds)> entries = new List<(MeshCollider collider, Bounds bounds)>();
    private float timer = 0;
    // 距离死区阈值
    public float enableRadius = 30.0f;
    public float disableRadius = 40.0f;

    /// <summary>每 0.2 秒按死区阈值刷新一次场景 MeshCollider 的启用状态</summary>
    private void UpdateMeshColliderTick()
    {
        timer += Time.deltaTime;
        if (timer < 0.2f) return;

        timer = 0;
        Vector3 center = transform.position;
        ColliderUtil.Tick(entries, center, enableRadius, disableRadius);
    }

    // 收集当前场景的碰撞
    public void CollectMeshCollider()
    {
        entries = ColliderUtil.CollectAll(-1);
        ColliderUtil.Tick(entries, transform.position, enableRadius, disableRadius);

        // 计时器清零，让 Update 里第一次 Tick 从这一刻开始重新算
        timer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, enableRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(center, disableRadius);
    }

    ////////////////////////////////////////死亡与复活////////////////////////////////////////

    private static List<IDeathAndRevive> deathAndReviveCallbacks = new List<IDeathAndRevive>();

    // 玩家死亡的时候创建的，用于屏幕变灰白
    // private Volume deathVolume;

    public static void RegisterDeathAndReviveEvents(IDeathAndRevive who)
    {
        if (!deathAndReviveCallbacks.Contains(who))
            deathAndReviveCallbacks.Add(who);
    }

    public static void UnregisterDeathAndReviveEvents(IDeathAndRevive who)
    {
        deathAndReviveCallbacks.Remove(who);
    }

    /// <summary>
    /// 调用后会立即死亡，不论为什么。适用于网络层接收到死亡消息后主动调用这个。
    /// </summary>
    public override void Dead()
    {
        base.Dead();

        // 快照遍历：回调中可能增删注册者，直接遍历会抛
        foreach (var who in new List<IDeathAndRevive>(deathAndReviveCallbacks))
        {
            who.OnPlayerDeath();
        }

        gameMainPanel.Hide();
    }

    /// 玩家复活，目前是直接原地复活
    public override void Revive()
    {
        if (!isDead) return;
        base.Revive();

        // 快照遍历：回调中可能增删注册者（如 GameMainPanel 重建），直接遍历会抛 Collection was modified
        foreach (var who in new List<IDeathAndRevive>(deathAndReviveCallbacks))
        {
            who.OnPlayerRevive();
        }

        // 发送复活消息
        var reviveMsg = new ClientRevive();

        // 客户端收到消息：成功复活则重建网格原地放一个，失败则是，都不会执行
        Context.net.Send(reviveMsg,0, LiteNetLib.DeliveryMethod.ReliableOrdered,() =>
        {
            // CharacterAnimation已经没用了，不用管他了
            // 新建一个新的LocalPlayer出来
            Context.sc.SpawnALocalPlayer(playerSaveData, transform.position);
            // possessNewCharacter在新玩家的Start里会自动调用，无需担心。
            // 老角色的characterAnimation和LocalPlayer都没用了，销毁以便减少性能
            Destroy(this);
            Destroy(characterAnimation);
            // 销毁相机
            Destroy(tpsCamera.gameObject);
        }, () => {});
    }

    /// <summary>
    /// 回家复活，暂时弃用，目前统一走Revive
    /// </summary>
    public void ReviveAtHome()
    {
        if (!isDead) return;
        // 和 Revive() 一样先走基类：清 isDead、把 moveState 置为 REVIVE，
        // 否则复活后角色仍是"死"的状态，moveState 一直停在 DEAD
        base.Revive();

        float reviveTime = characterAnimation.GetReviveAnimTime();

        // 快照遍历：回调中可能增删注册者（如 GameMainPanel 重建），直接遍历会抛 Collection was modified
        foreach (var who in new List<IDeathAndRevive>(deathAndReviveCallbacks))
        {
            who.OnPlayerRevive();
        }

        // RegisterDelayTask 兜底：若动画自检未触发（如状态名不匹配），到时仍恢复
        Context.updateProxy.RegisterDelayTask(() =>
        {
            characterAnimation.OnReviveFinished();
            characterAnimation.onReviveFinished?.Invoke();
            characterAnimation.onReviveFinished = null;
        }, reviveTime);

        Context.sc.EnterNewScene(Context.sc.currentScene, true);
        gameMainPanel.Show();
    }

    /// <summary>
    /// 设置本地读档数据
    /// </summary>
    public void SetSaveData(SaveData saveData)
    {
        if (saveData == null)
        {
            PK.Log.Error("SetSaveData收到了null，这不该发生！");
            return;
        }

        if (inventory == null)
        {
            PK.Log.Error("玩家身上的存储系统为null！这不该发生！");
            return;
        }

        // 比较服务端发来的SaveData是否一样，一样则不执行任何操作
        if (saveData.IsSameAs(playerSaveData)) return;

        bool genderChanged = isGirl != saveData.isGirl;

        playerSaveData = saveData;
        isGirl = saveData.isGirl;
        equipSystem.isGirl = isGirl;

        // Start 之前 equipSystem 还没 Initialize，此时渲染会把骨骼搞乱
        if (equipReady)
            inventory.BindSaveData(saveData);

        if (genderChanged && equipReady && characterAnimation != null)
            characterAnimation.SetGender(isGirl);
    }

    ///////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////输入事件////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////
    public void OnSpiritPressed()
    {
        StartRun();
    }

    public void OnSpiritReleased()
    {
        StopRun();
    }

    // 鼠标按住Alt（实际显隐由 UIManager.RefreshCursorState 统一决策：
    // UI栈非空 或 按住Alt 时显示，否则隐藏锁定；此处仅触发刷新）
    public void OnAltPressed()
    {
        Context.um?.RefreshCursorState();
    }

    public void OnAltReleased()
    {
        Context.um?.RefreshCursorState();
    }

    public void OnLeftMouseKeyPressed()
    {
        var res = LineTraceSceneObject(GetCamera());
        if (res == null)
        {
            // gameMainPanel?.ShowSceneObjectInfo((null,null,null,EInteractableType.Player,this));
            return;
        }
        gameMainPanel?.ShowSceneObjectInfo(res.OnRaycastHit());
    }

    public void OnRightMouseKeyPressed()
    {
        var res = Actor.LineTraceSceneObject(GetCamera());
        var casted = res as SceneObjectBase;
        if (res == null || casted == null || !casted.canInteract) return;

        new FurnitureRightClickMenu(casted).Show();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (Context.im.GetController() != (IControlable)this)
        {
            Move(Vector3.zero);
        }
    }

    public void OnFixedMove(Vector2 direction)
    {
        if (isDead) return;

        if (Mathf.Abs(direction.x) > 0.01f || Mathf.Abs(direction.y) > 0.01f) {
            Transform cam = tpsCamera.TPCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;

            Vector3 moveDir = (forward * direction.y + right * direction.x).normalized;
            float speed = isRunPressed ? maxRunSpeed : maxWalkSpeed;
            Move(moveDir * speed);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(moveDir),
                rotateSpeed * Time.deltaTime);

            if (property.health <= 0 || !isRunPressed) return;
            property.ConsumeStamina(EActionType.Run);

        }
        else
        {
            Move(Vector3.zero);
        }
    }

    public void OnMouseMove(Vector2 direction)
    {
        if (isDead || Cursor.lockState == CursorLockMode.None) return;
        tpsCamera.RotateCamera(direction);
    }

    public void OnMouseWheelMoved(float scrollDelta)
    {
        if (isDead) return;
        tpsCamera.Zoom(scrollDelta);
    }

    public void OnOpenBagPressed()
    {
        OpenBag();
        // Chat已分离为独立UI，不需再关闭
    }

    public void OnExitPressed()
    {
        // 后续将加入暂停菜单。
    }

    public void OnJumpPressed()
    {
        if (property.health >= 10)
        {
            property.ConsumeStamina(EActionType.Jump);
            Jump();
        }
    }

    public void OnBeginChatPressed()
    {
        var chat = new ChatPanel();
        chat.Show();
        chat.messageInput.text += "/";
    }
    ///////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////输入事件////////////////////////////////////////
    ///////////////////////////////////////////////////////////////////////////////////////

    ////////////////////////////////////////场景交互////////////////////////////////////////

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return ("Player", "一个玩家。话说你为什么能看到他？这根本不该发生。", "ui/icon/sceneobjecticon/npc/Furniture_Npc_0001", EInteractableType.Player,this);
    }

    public override void StartInteract(Vector3 playerPos, CharacterBase interactChar)
    {
        return;
    }
}
