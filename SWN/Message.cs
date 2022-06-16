public enum MsgType
{
    TOKEN = 1,
    ACK = 2
}

public record Message(int Port, int Value, MsgType Type)
{
    public override string ToString()
    {
        return $"{this.Port}:{this.Value}:{this.Type}";
    }
}