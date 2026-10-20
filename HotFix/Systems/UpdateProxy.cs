using System;
using System.Collections.Generic;
using UnityEngine;

public class UpdateProxy : MonoBehaviour
{
    private class TaskData
    {
        public float remaining;
        public float gap;
        public float accumulator;
    }

    private Dictionary<Action, TaskData> tasks = new Dictionary<Action, TaskData>(5);
    private List<Action> fixedTasks = new List<Action>();
    private Dictionary<Action, float> delayedTask = new Dictionary<Action, float>(5);
    private List<Action> keysSnapshot = new List<Action>(5);

    // 下一帧任务
    private List<Action> nextFrameTasks = new List<Action>(5);
    private List<Action> nextFrameSnapshot = new List<Action>(5);

    void Update()
    {
        keysSnapshot.Clear();
        keysSnapshot.AddRange(tasks.Keys);

        foreach (var key in keysSnapshot)
        {
            if (!tasks.TryGetValue(key, out var task))
                continue;

            task.remaining -= Time.deltaTime;
            task.accumulator += Time.deltaTime;

            if (task.gap <= 0 || task.accumulator >= task.gap)
            {
                key?.Invoke();
                task.accumulator = task.gap <= 0 ? 0 : task.accumulator - task.gap;
            }

            if (task.remaining <= 0)
            {
                tasks.Remove(key);
            }
        }

        keysSnapshot.Clear();
        keysSnapshot.AddRange(delayedTask.Keys);
        foreach (var key in keysSnapshot)
        {
            if (delayedTask.TryGetValue(key, out float time))
            {
                time -= Time.deltaTime;
                if (time <= 0)
                {
                    key?.Invoke();
                    delayedTask.Remove(key);
                }
                else delayedTask[key] = time;
            }
        }

        // 下一帧完成的任务立刻完成
        if (nextFrameTasks.Count > 0)
        {
            nextFrameSnapshot.Clear();
            nextFrameSnapshot.AddRange(nextFrameTasks);
            nextFrameTasks.Clear();
            for (int i = 0; i < nextFrameSnapshot.Count; i++)
            {
                nextFrameSnapshot[i]?.Invoke();
            }
        }
    }

    void FixedUpdate()
    {
        foreach (var t in fixedTasks)
        {
            t?.Invoke();
        }
    }

    public void RegisterFixedTask(Action newAct)
    {
        foreach (var t in fixedTasks)
        {
            if (t == newAct) return;
        }

        fixedTasks.Add(newAct);
    }

    public void RegisterNewTask(Action newAct, float operateTime, float gap = 0)
    {
        if (tasks.TryGetValue(newAct, out var task))
        {
            task.remaining = operateTime;
            task.gap = gap;
            task.accumulator = 0;
        }
        else
        {
            tasks.Add(newAct, new TaskData
            {
                remaining = operateTime,
                gap = gap,
                accumulator = 0
            });
        }
    }

    public void RegisterDelayTask(Action newAct, float delayTime)
    {
        if (newAct == null) return;

        if (delayedTask.ContainsKey(newAct))
        {
            delayedTask[newAct] = delayTime;
            return;
        }
        else
        {
            delayedTask.Add(newAct, delayTime);
        }
    }

    /// <summary>
    /// 推迟到下一帧立刻完成：本帧只入队，下一帧Update开头统一执行。
    /// </summary>
    public void RegisterNextFrameTask(Action newAct)
    {
        if (newAct == null) return;

        for (int i = 0; i < nextFrameTasks.Count; i++)
        {
            if (nextFrameTasks[i] == newAct) return;
        }

        nextFrameTasks.Add(newAct);
    }

    public void DestoryTask(Action targetAction)
    {
        tasks.Remove(targetAction);
    }
}
