using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace PKWeb
{
    public static class CmdUtil
    {
        public static void ServerExecute(ServerConnect host ,string Cmd)
        {
            switch (Cmd)
            {
                case "help":
                    {
                        Console.WriteLine("可用命令：");
                        Console.WriteLine("  help         显示帮助");
                        Console.WriteLine("  clear        清屏");
                        Console.WriteLine("  playercount  查看当前在线人数");
                        Console.WriteLine("  exit         关闭服务器");
                        break;
                    }
                case "clear":
                    {
                        Console.Clear();
                        break;
                    }
                case "playercount":
                    {
                        Console.WriteLine(host.roomMgr.GetPlayerCount());
                        break;
                    }
                case "exit":
                    {
                        host.StopServer();
                        break;
                    }
                default:
                    {
                        Console.WriteLine("未知命令，请使用help查询");
                        return;
                    }
            }
        }

        public static void ClientExecute(ClientConnect client, string Cmd)
        {
            switch (Cmd)
            {
                case "help":
                    {
                        Console.WriteLine("可用命令：");
                        Console.WriteLine("  help         显示帮助");
                        Console.WriteLine("  clear        清屏");
                        Console.WriteLine("  playercount  查看服务器在线人数");
                        Console.WriteLine("  exit         断开连接");
                        break;
                    }
                case "clear":
                    {
                        Console.Clear();
                        break;
                    }
                    // 发送请求，打印数据
                case "playercount":
                    {
                        var w = new NetDataWriter();
                        var req = new PlayerCountRequest();
                        client.processor.WriteNetSerializable(w, ref req);
                        client.netPeer.Send(w, 0, DeliveryMethod.ReliableOrdered);
                        break;
                    }
                case "exit":
                    {
                        client.Disconnect();
                        break;
                    }
                case "login":
                    {

                        break;
                    }
                default:
                    {
                        Console.WriteLine("未知命令，请使用help查询");
                        return;
                    }
            }
        }


    }
}
