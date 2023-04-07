module ProcessService

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open System.Text

open MessageModule

type ProcessService(leftNeighPort:int, myPort:int, rigthNeighPort:int) = 

    let mutable _newToken: int = 0
    let mutable _myToken: int = 0
    let mutable _ack: int = 0
    
    let mutable _tkn: bool = false
    let _prevPort: int = leftNeighPort
    let _nextPort: int = rigthNeighPort
    let _port = myPort

    member  this.AddStartToken() : ProcessService = 
        _tkn <- true
        this

    member this.UdpListenAsync() = 
        async{
            let _listener = new UdpClient(_port)
            let _sender = new UdpClient()
            let rng = new Random(_port)

            new IPEndPoint(IPAddress.Loopback, _nextPort) |> _sender.Connect

            while true do
                try
                    _listener.ReceiveAsync().ContinueWith( fun (result: Task<UdpReceiveResult>) -> 
                        let datagram = result.Result
                        let values = Encoding.ASCII.GetString(datagram.Buffer, 0, datagram.Buffer.Length).Split(':')
                        let msg = new Message(  Int32.Parse(values[0]), 
                                                Int32.Parse(values[1]),
                                                Enum.Parse<MsgType>(values[2]) )
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
                    
                    ) |> Async.AwaitTask |> ignore
                with 
                | ex -> printfn "Exception: %s" ex.Message
        }

    member this.TokenRingAlgorithmAsync() =
        async{
            let rng = new Random(_port)
            let _sender = new UdpClient()
            _sender.Connect(IPAddress.Loopback, _nextPort)

            if _tkn = true then
                _myToken <- 1
                do! Async.Sleep 5  
                if rng.NextDouble() > StaticHelper.BreakConnectionLimit then 
                    let msg = new Message(_nextPort, 1, MsgType.TOKEN)
                    let buffer = msg.ToString() |> Encoding.ASCII.GetBytes 
                    (buffer, buffer.Length) 
                    |> _sender.SendAsync 
                    |> Async.AwaitTask |> ignore

                    (_port, _myToken) ||> printfn "%d: Send TOKEN %d"
            while _myToken < 51 do
                if _myToken = 0 && _newToken = 0 then
                    //nie mam tokenu oraz nie dostalem nowego, spimy dalej
                    do! Async.Sleep 5   
                else
                    do! Async.Sleep StaticHelper.Timeout 
                    if _ack = _myToken && _myToken <> 0 then
                        // otrzymalem ACK na wyslanie tokenu, dotarl, moge usunac token z pamieci
                        (_port, _myToken) ||> printfn "%d: Get ACK for sent TOKEN %d"
                        _myToken <- 0
                    else if _newToken > _myToken && _newToken > _ack then
                        //odebrano nowy token
                        (_port, _newToken) ||> printfn "%d: Get new TOKEN %d"
                        //wyslij ACK odbioru
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then 
                            let msg = new Message(_prevPort, _newToken, MsgType.ACK)
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            (_port, _newToken) ||> printfn "%d: Send ACK %d"
                        // utworz moj token
                        _myToken <- _newToken + 1
                        _newToken <- 0
                        //symuluj dzialanie
                        do! Async.Sleep 5 
                        //wyslij token dalej (skonczylem przetwarzac)
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            let msg = new Message(_nextPort, _myToken, MsgType.TOKEN)
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            (_port, _myToken) ||> printfn "%d: Send TOKEN %d"
                    else if _myToken <> 0 then
                        // nie dostalem nowego tokenu oraz nie dostalem ACK, wysylam ponownie
                        // druga opcja, dostalem stary zeton, ale mam _ack nowsze
                        if rng.NextDouble() > StaticHelper.BreakConnectionLimit then
                            let msg = Message(_nextPort, _myToken, MsgType.TOKEN)
                            let buffer = msg.ToString() |> Encoding.ASCII.GetBytes 
                            (buffer, buffer.Length) 
                            |> _sender.SendAsync 
                            |> Async.AwaitTask |> ignore
                            (_port, _myToken) ||> printfn "%d: Resend TOKEN %d"
        }