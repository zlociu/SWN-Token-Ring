module ProcessService

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open System.Text

open MessageModule

type ProcessService(leftNeighPort:int, myPort:int, rigthNeighPort:int, startToken:bool) = 

    let mutable _newToken: int = 0
    let mutable _myToken: int = 0
    let mutable _ack: int = 0
    
    let _prevPort: int = leftNeighPort
    let _nextPort: int = rigthNeighPort
    let _tkn: bool = startToken
    let _port = myPort

    member this.Port with get() = _port

    member this.UdpListen() = 
        async{
            Task.Yield() |> ignore
            let _listener = new UdpClient(_port)
            let _sender = new UdpClient()
            let rng = new Random(_port)

            new IPEndPoint(IPAddress.Loopback, _nextPort) |> _sender.Connect

            while true do
                try
                    let! datagram = _listener.ReceiveAsync() |> Async.AwaitTask
                    let values = Encoding.ASCII.GetString(datagram.Buffer, 0, datagram.Buffer.Length).Split(':')
                    let msg = new Message(  Int32.Parse(values.[0]), 
                                            Int32.Parse(values.[1]), 
                                            Enum.Parse(typeof<MsgType>, values.[2]) :?> MsgType)
                    if msg.Port <> _port then
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

    member this.TokenRingAlgorithm() =
        async{
            Task.Yield() |> ignore
            let rng = new Random(_port)
            let _sender = new UdpClient()
            _sender.Connect(IPAddress.Loopback, _nextPort)

            if _tkn = true then
                _myToken <- 1
                Thread.Sleep 5
                let msg = new Message(_nextPort, 1, MsgType.TOKEN)
                if rng.NextDouble() > StaticHelper.BreakConnectionLimit then 
                    let buffer = msg.ToString() |> Encoding.ASCII.GetBytes 
                    (buffer, buffer.Length) 
                    |> _sender.SendAsync 
                    |> Async.AwaitTask |> ignore

                    (_port, _myToken) ||> printfn "%d: Send TOKEN %d"
            while _myToken < 51 do
                if _myToken = 0 && _newToken = 0 then
                    //nie mam tokenu oraz nie dostalem nowego, spimy dalej
                    Thread.Sleep 5
                else
                    StaticHelper.Timeout |> Thread.Sleep 
                    if _ack = _myToken && _myToken <> 0 then
                        // otrzymalem ACK na wyslanie tokenu, dotarl, moge usunac token z pamieci
                        (_port, _myToken) ||> printfn "%d: Get ACK for sent TOKEN %d"
                        _myToken <- 0
                    else if _newToken > _myToken && _newToken > _ack then
                        //odebrano nowy token
                        (_port, _newToken) ||> printfn "%d: Get new TOKEN %d"
                        //wyslij ACK odbioru
                        let msg = new Message(_prevPort, _newToken, MsgType.ACK)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then 
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            (_port, _newToken) ||> printfn "%d: Send ACK %d"
                        // utworz moj token
                        _myToken <- _newToken + 1
                        _newToken <- 0
                        //sumuluj dzialanie
                        Thread.Sleep(5)
                        //wyslij token dalej (skonczylem przetwarzac)
                        let msg2 = new Message(_nextPort, _myToken, MsgType.TOKEN)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            (_port, _myToken) ||> printfn "%d: Send TOKEN %d"
                    else if _myToken <> 0 then
                        // nie dostalem nowego tokenu oraz nie dostalem ACK, wysylam ponownie
                        // druga opcja, dostalem stary zeton, ale mam _ack nowsze
                        let msg = Message(_nextPort, _myToken, MsgType.TOKEN)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            (_port, _myToken) ||> printfn "%d: Resend TOKEN %d"
        }