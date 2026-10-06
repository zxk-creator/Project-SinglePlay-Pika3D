using System.Collections.Generic;
using LiteNetLib.Utils;
using PKSv;
using UnityEngine;

namespace PKWeb;

// 客户端发给服务端
public class LoginRequest : INetSerializable
{
    public SaveData player;

    public void Serialize(NetDataWriter writer)
    {
        player.Serialize(writer);
    }

    public void Deserialize(NetDataReader reader)
    {
        player = new SaveData();
        player.Deserialize(reader);
    }
}

// 服务端发给客户端
public class LoginResponse : INetSerializable
{
    public enum Status
    {
        ACCEPTED,
        REJECT_PLAYERFULL,
        REJECTED_SAME_NAME,
    }

    public Status serverResponse;
    
    public Vector3 spawnPoint;
    public ESceneType enterScene;
    // 只发你要进的那个场景的玩家
    public List<NetPlayerData> scenePlayers;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((int)serverResponse);
        writer.Put(spawnPoint.x);
        writer.Put(spawnPoint.y);
        writer.Put(spawnPoint.z);
        writer.Put((int)enterScene);

        if (scenePlayers == null)
        {
            writer.Put(0);
        }
        else
        {
            writer.Put(scenePlayers.Count);
            foreach (var p in scenePlayers)
                p.Serialize(writer);
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        serverResponse = (Status)reader.GetInt();
        spawnPoint = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        enterScene = (ESceneType)reader.GetInt();

        int count = reader.GetInt();
        scenePlayers = new List<NetPlayerData>(count);
        for (int i = 0; i < count; i++)
        {
            var p = new NetPlayerData();
            p.Deserialize(reader);
            scenePlayers.Add(p);
        }
    }
}