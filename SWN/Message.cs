public enum MsgType
{
    TOKEN = 1,
    ACK = 2
}

public record Message
{
    public int Value {get; init;}
    public MsgType Type {get; init;}
    public int Port {get; init;}

    public Message(int port, int value, MsgType type)
    {
        Value = value;
        Type = type;
        Port = port;
    }

    public override string ToString()
    {
        return $"{this.Port}:{this.Value}:{this.Type}";
    }
}