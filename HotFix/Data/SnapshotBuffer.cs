
using System.Collections.Generic;
using PK;
using UnityEngine;

public struct TransformSyncSnapshot
{
    // 真正的位置同步消息
    public OtherPlayerLocationSync sync;
    // 接收到的时间戳
    public float receivedTime;
    public bool isValid = false;

    // 适用于无效构造，读者要自行根据isValid判断是否有效
    public TransformSyncSnapshot() {}

    public TransformSyncSnapshot(OtherPlayerLocationSync sync, float receivedTime)
    {
        this.sync = sync;
        this.receivedTime = receivedTime;
        isValid = true;
    }
}

/// <summary>
/// 每个NetPlayer都各自维护一个缓冲区，用于接收服务端发来的位置同步信号并进行平滑插值
/// </summary>
public class LocationSyncBuffer
{
    private List<TransformSyncSnapshot> buffer = new List<TransformSyncSnapshot>();
    private TransformSyncSnapshot lastShot;

    private float readDuration = 0.1f;
    private float readDelay = 0.05f;
    private float readLast;
    // 是否是首次调用
    private bool isInitialized = false;

    // 添加一个新的位置同步消息
    public void AddOneSync(TransformSyncSnapshot newSync)
    {
        buffer.Add(newSync);
    }

    // 读取一个消息，外部读取这个进行位置插值
    public TransformSyncSnapshot ReadSync()
    {
        // 没有位置同步消息，返回无效构造的对象
        if (buffer.Count == 0) return new TransformSyncSnapshot();

        // 如果是首次调用
        if (!isInitialized)
        {
            lastShot = buffer[0];
            isInitialized = true;
            readLast = Time.realtimeSinceStartup;
            return lastShot;
        }

        // 如果快照数量不够
        if (buffer.Count < 2)
        {
            readLast = Time.realtimeSinceStartup;
            return lastShot;
        }

        // 如果堆积太多，跳到最新（会导致瞬移）
        if (buffer.Count > 5)
        {
            lastShot = buffer[buffer.Count - 1];
            buffer.Clear();
            buffer.Add(lastShot);
            readLast = Time.realtimeSinceStartup;
            return lastShot;
        }

        // 一个插值完成，跳到下一段
        if (Time.realtimeSinceStartup - readLast > readDuration)
        {
            lastShot = buffer[0];
            buffer.RemoveAt(0);
            readLast += readDuration;
        }

        // 快照刚到，需等待一段时间方便有下一个插值
        if (Time.realtimeSinceStartup - buffer[0].receivedTime < readDelay)
        {
            readLast = Time.realtimeSinceStartup;
            return lastShot;
        }

        // 插值
        float t = Mathf.Clamp01((Time.realtimeSinceStartup - readLast) / readDuration);

        var a = lastShot.sync;
        var b = buffer[0].sync;

        var result = new TransformSyncSnapshot
        {
            receivedTime = buffer[0].receivedTime,
            isValid = true,
            sync = new OtherPlayerLocationSync
            {
                playerName = b.playerName,
                location = Vector3.Lerp(a.location, b.location, t),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
                currentAnimation = b.currentAnimation,
            }
        };
        return result;
    }

    public void Clear()
    {
        buffer.Clear();
        lastShot = new TransformSyncSnapshot();
        readLast = 0f;
        isInitialized = false;
    }
}