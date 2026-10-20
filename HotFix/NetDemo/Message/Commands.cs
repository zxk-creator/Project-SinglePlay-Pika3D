using LiteNetLib.Utils;

// 客户端发到服务端请求执行的命令，这些命令由服务端权威的服务端批准执行
// 注意，此类仅适用于客户端发到服务端，服务端批准后才会真的执行。客户端通过收到消息执行具体操作
public class Commands : INetSerializable
{
    // TODO: 目前任何人都能执行任何命令，需要改成只有主机或管理员才能执行命令
    public string command;
    public void Serialize(NetDataWriter writer)
    {
        writer.Put(command);
    }

    public void Deserialize(NetDataReader reader)
    {
        command = reader.GetString();
    }
}