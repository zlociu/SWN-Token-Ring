module MessageModule

type MsgType = 
|   TOKEN = 1
|   ACK = 2


type Message(port:int, value:int, msgType:MsgType) = 
    member val Value = value with get, set 
    member val Type = msgType with get, set
    member val Port = port with get, set 

    new(port:int, value:int) = 
        Message(port, value, MsgType.TOKEN)

    override this.ToString() = 
        $"{this.Port}:{this.Value}:{this.Type}"

