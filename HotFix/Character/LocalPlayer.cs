using System.Collections.Generic;
using PKSv;
using UnityEngine;
using UnityEngine.Rendering;

public class LocalPlayer : CharacterBase, IInputAcceptable, IControlable
{
    [Header("旋转")]
    [Tooltip("角色转向平滑速度")]
    public float rotateSpeed = 10f;
    public TFPSCamera tpsCamera {get; private set;}
    private GameMainPanel gameMainPanel;
    // 角色背包系统
    public InventorySystem inventory {get; private set;}
    private static List<IDeathAndRevive> deathAndReviveCallbacks = new List<IDeathAndRevive>();
    private BagPanel bagPanel;

    // 存档的数据，注意仅本地！不是NetPlayerData。为什么？因为这些东西是存在本地的！要一层网络封装没什么用！
    public SaveData playerSaveData;

    private float lastSynchronizeTime = 0;
    public CharacterAnimation animation;

    // 玩家死亡的时候创建的，用于屏幕变灰白
    // private Volume deathVolume;

    private void Awake()
    {
        if (equipSystem == null) equipSystem = GetComponentInChildren<Equip>();
        equipSystem.isGirl = isGirl;
        equipSystem.isNPC = false;
        // 获得他的RigidBody组件，设置为运动学，防止场景没生成的时候往下掉，我们让他加载好后再说
        SetRigidbodyKinematic(true);

        animation = GetComponent<CharacterAnimation>();

        // 游戏开始时，取消骨骼的碰撞，原本预留用于布娃娃的，但是实际上没必要
        Transform root = equipSystem.bonesRoot;
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        foreach (Rigidbody r in root.GetComponentsInChildren<Rigidbody>(true))
        {
            r.detectCollisions = false;
            r.isKinematic = true;
        }
    }

    protected override void Start()
    {
        // 建骨架之前就要定好性别：CharacterBase.Start 是按 isGirl 挑骨架 prefab 的，
        // 骨架一旦 Instantiate 出来就改不了，所以读档必须在 base.Start() 之前套用
        if (playerSaveData != null)
            isGirl = playerSaveData.isGirl;
        equipSystem.SetGender(isGirl);

        base.Start();

        inventory = new InventorySystem(equipSystem, this);
        // 玩家仅注册一次
        gameMainPanel = new GameMainPanel();
        gameMainPanel.Show();
        // 不销毁，除非退出到主菜单。
        Context.localPlayer = this;

        tpsCamera = GetComponent<TFPSCamera>();
        tpsCamera.TPSCameraTarget = gameObject.transform;

        Context.im.PossessNewChracter(this);

        if (playerSaveData != null)
        {
            // 读档：直接套用存档里的装备/工具/背包，不走"新角色随机一套"的流程
            inventory.SetCharacterEquips(playerSaveData.equipedCloth);
            inventory.SetTool(playerSaveData.currentEquipedTool as ToolBase);
            // 新档这几项就是 null（NewSavePanel 建档时写死 null），SetBag(null) 会报空指针。
            // 没背包不是异常，跳过即可。
            if (playerSaveData.equipedBag != null)
                inventory.SetBag(playerSaveData.equipedBag);
            if (playerSaveData.bagpackItems != null)
                inventory.BagSpace?.ReplaceItems(playerSaveData.bagpackItems);
            if (playerSaveData.handItems != null)
                inventory.HandSpace.ReplaceItems(playerSaveData.handItems);
        }
        else
        {
            // 新角色：默认装 + 空背包 + 随机衣服
            inventory.equipFrontend.ApplyDefaultOutfit();
            inventory.ApplyEquipsToRender();

            var bagTemplate = Context.Item.allItems[R.JsonName.jsonBagNode][0];
            inventory.SetBag((BagpackBase)bagTemplate.GetACopy());

            inventory.SetCharacterEquips(NPCUtil.GenerateRandomCloth(isGirl));
        }
    }

    protected override void Update()
    {
        base.Update();
        if (property.health <= 0)
            StopRun();
        
        // 计时：每0.1秒发送一次位置同步信号
        lastSynchronizeTime += Time.deltaTime;
        if (lastSynchronizeTime >= 0.1)
        {
            // 必须清零，否则时间只增不减，之后每帧都会发一次
            lastSynchronizeTime = 0f;

            // 发送位置同步信号
            var msg = new LocationSynchronize()
            {
                location = transform.position,
                rotation = transform.rotation,
                currentAnimation = animation.GetPlayingAnimationType()
            };

            // 1: 高频丢了无所谓的，像位置同步这样连续的
            Context.net.Send(msg, 1, LiteNetLib.DeliveryMethod.Sequenced);
        }
    }

    void LateUpdate()
    {
        Shader.SetGlobalVector("_UTSDitherCameraPos", tpsCamera.GetActiveCamera().transform.position);
    }

    public Camera GetCamera()
    {
        return tpsCamera.GetActiveCamera();
    }

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

    // 原地复活
    public override void Revive()
    {
        if (!isDead) return;
        base.Revive();
        Time.timeScale = 1f;
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

        gameMainPanel.Show();
    }

    // 回家复活
    public void ReviveAtHome()
    {
        if (!isDead) return;
        // 和 Revive() 一样先走基类：清 isDead、把 moveState 置为 REVIVE，
        // 否则复活后角色仍是"死"的状态，moveState 一直停在 DEAD
        base.Revive();
        Time.timeScale = 1f;

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
    /// 下面都是输入事件
    /// </summary>
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
        var res = Actor.LineTraceSceneObject(GetCamera());
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
        else Move(Vector3.zero);
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
    /// <summary>
    /// 输入钩子结束
    /// </summary>

    public void OpenBag()
    {
        bagPanel = new BagPanel(inventory);
        bagPanel.Show();
    }

    public static void RegisterDeathAndReviveEvents(IDeathAndRevive who)
    {
        if (!deathAndReviveCallbacks.Contains(who))
            deathAndReviveCallbacks.Add(who);
    }

    public static void UnregisterDeathAndReviveEvents(IDeathAndRevive who)
    {
        deathAndReviveCallbacks.Remove(who);
    }

    public override (string name, string description, string iconPath, EInteractableType interactableType, Actor selfRef) OnRaycastHit()
    {
        return ("Player", "一个玩家。话说你为什么能看到他？这根本不该发生。", "ui/icon/sceneobjecticon/npc/Furniture_Npc_0001", EInteractableType.Player,this);
    }

    public override void StartInteract(Vector3 playerPos, CharacterBase interactChar)
    {
        return;
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

}
