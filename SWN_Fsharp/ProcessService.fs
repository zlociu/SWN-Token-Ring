module ProcessService

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open System.Text

open MessageModule

type ProcessService(prevPort:int, port:int, nextPort:int, tkn:bool) = 

    let mutable _newToken: int = 0
    let mutable _myToken: int = 0
    let mutable _ack: int = 0
    member val PrevPort: int = prevPort with get, set
    member val Port: int = port with get, set
    member val NextPort: int = nextPort with get, set
    member val Tkn: bool = tkn with get, set 

    member this.udpListen (port:int) (nextPort:int) = 
        async{
            Task.Yield() |> ignore
            let _listener = new UdpClient(port)
            let _sender = new UdpClient()
            let rng = new Random(port)

            new IPEndPoint(IPAddress.Loopback, nextPort) |> _sender.Connect

            while true do
                try
                    let! datagram = _listener.ReceiveAsync() |> Async.AwaitTask
                    let values = Encoding.ASCII.GetString(datagram.Buffer, 0, datagram.Buffer.Length).Split(':')
                    let msg = new Message(  Int32.Parse(values.[0]), 
                                            Int32.Parse(values.[1]), 
                                            Enum.Parse(typeof<MsgType>, values.[2]) :?> MsgType)
                    if msg.Port <> port then

                        //to nie jest wiadomosc do mnie, wysylam dalej
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            _sender.SendAsync(datagram.Buffer, datagram.Buffer.Length) 
                            |> Async.AwaitTask |> ignore    
                    else
                        match msg.Type with 
                        | MsgType.TOKEN -> 
                            if _newToken < msg.Value && _myToken < msg.Value then 
                                _newToken <- msg.Value
                        | _ ->
                            if _ack < msg.Value then 
                                _ack <- msg.Value
                with 
                | ex -> printfn "Exception: %s" ex.Message
        }

    member this.tokenRingAlgorithm prevPort port nextPort tkn =
        async{
            Task.Yield() |> ignore
            let rng = new Random(port)
            let _sender = new UdpClient()
            _sender.Connect(IPAddress.Loopback, nextPort)

            if tkn = true then
                _myToken <- 1
                Thread.Sleep 5
                let msg = new Message(nextPort, 1, MsgType.TOKEN)
                if rng.NextDouble() > StaticHelper.BreakConnectionLimit then 
                    let buffer = msg.ToString() |> Encoding.ASCII.GetBytes 
                    (buffer, buffer.Length) 
                    |> _sender.SendAsync 
                    |> Async.AwaitTask |> ignore

                    (port, _myToken) ||> printfn "%d: Send TOKEN %d"
            while _myToken < 51 do
                if _myToken = 0 && _newToken = 0 then
                    
                    //nie mam tokenu oraz nie dostalem nowego, spimy dalej
                    Thread.Sleep 5
                else
                    StaticHelper.Timeout |> Thread.Sleep 
                    if _ack = _myToken && _myToken <> 0 then
                       
                        // otrzymalem ACK na wyslanie tokenu, dotarl, moge usunac token z pamieci
                        (port, _myToken) ||> printfn "%d: Get ACK for sent TOKEN %d"
                        _myToken <- 0
                    else if _newToken > _myToken && _newToken > _ack then // && ((_myToken != 0 && tkn == 1) || tkn == 0))
                        
                        //odebrano nowy token
                        (port, _newToken) ||> printfn "%d: Get new TOKEN %d"

                        //wyslij ACK odbioru
                        let msg = new Message(prevPort, _newToken, MsgType.ACK)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then 
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore

                            (port, _newToken) ||> printfn "%d: Send ACK %d"
                        // utworz moj token
                        _myToken <- _newToken + 1
                        _newToken <- 0

                        //sumuluj dzialanie
                        Thread.Sleep(5)

                        //wyslij token dalej (skonczylem przetwarzac)
                        let msg2 = new Message(nextPort, _myToken, MsgType.TOKEN)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            
                            (port, _myToken) ||> printfn "%d: Send TOKEN %d"
                    else if _myToken <> 0 then

                        // nie dostalem nowego tokenu oraz nie dostalem ACK, wysylam ponownie
                        // druga opcja, dostalem stary zeton, ale mam _ack nowsze
                        let msg = Message(nextPort, _myToken, MsgType.TOKEN)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore

                            (port, _myToken) ||> printfn "%d: Resend TOKEN %d"
        }