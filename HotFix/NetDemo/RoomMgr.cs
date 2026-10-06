using System.Collections.Generic;
using LiteNetLib;
using PKSv;

namespace PKWeb
{
    // 全局的“大房间”，也就是一个世界，管理着所有人。只有服务端才持有这个
    public class RoomMgr
    {
        public Dictionary<ESceneType, List<NetPlayerData>> roomAndPlayers {get; private set;} = new Dictionary<ESceneType, List<NetPlayerData>>();
        private int currentTime = 8 * 60;
        public SaveData currentWorld;

        public RoomMgr(SaveData world)
        {
            // 初始化各种房间的玩家列表，否则 AddPlayer 取下标会抛 KeyNotFoundException
            roomAndPlayers[ESceneType.CITY] = new List<NetPlayerData>();
            roomAndPlayers[ESceneType.DEFAULT] = new List<NetPlayerData>();
            currentWorld = world;

            Context.updateProxy.RegisterNewTask(TickTime, int.MaxValue, 1f);
        }

        // 游戏内时钟每秒走一分钟
        private void TickTime()
        {
            currentTime = (currentTime + 1) % 1440;
        }

        /// <summary>停止游戏内时钟。RoomMgr不再使用时调用。</summary>
        public void StopTimer()
        {
            Context.updateProxy.DestoryTask(TickTime);
        }

        public string GetCurrentTime()
        {
            int hour = currentTime / 60;
            int minute = currentTime % 60;
            return $"{hour:D2}:{minute:D2}";
        }

        public int GetPlayerCount()
        {
            int total = 0;
            foreach (var kv in roomAndPlayers)
                total += kv.Value.Count;
            return total;
        }

        public string[] GetPlayerNames()
        {
            List<string> names = new List<string>();
            foreach (var kv in roomAndPlayers)
            {
                foreach (var player in kv.Value)
                {
                    names.Add(player.playerData.playerName);
                }
            }

            return names.ToArray();
        }

        public void AddPlayer(ESceneType targetRoom, NetPlayerData newPlayer)
        {
            roomAndPlayers[targetRoom].Add(newPlayer);
        }

        public void RemovePlayer(ESceneType targetRoom, NetPlayerData targetPlayer)
        {
            roomAndPlayers[targetRoom].Remove(targetPlayer);
        }

        /// <summary>
        /// 按连接把玩家从所在房间移除（用于断线清理）。
        /// 遍历所有房间，因为一个 peer 只会存在于一个房间里，不关心具体是哪个。
        /// </summary>
        /// <returns>是否真的移除了一个玩家</returns>
        public bool RemovePlayerByPeer(NetPeer peer)
        {
            foreach (var kv in roomAndPlayers)
            {
                List<NetPlayerData> players = kv.Value;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].clientConnect != peer) continue;

                    players.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void RemoveAllPlayer()
        {
            foreach (var kv in roomAndPlayers)
                kv.Value.Clear();
        }

        public NetPlayerData FindPlayerByPeer(NetPeer peer)
        {
            foreach (var kv in roomAndPlayers)
            {
                foreach (var p in kv.Value)
                {
                    if (p.clientConnect == peer) return p;
                }
            }
            return null;
        }
    }
}
