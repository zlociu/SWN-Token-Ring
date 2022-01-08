using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class ProcessService
{
    private int _newToken;
    private int _myToken;
    private int _ack;
    private int _prevPort; 
    private int _port; 
    private int _nextPort;
    private int _tkn;

    public int Port {get => _port;}

    public ProcessService(int prevPort, int port, int nextPort, int tkn)
    {
        _prevPort = prevPort;
        _port = port;
        _nextPort = nextPort;
        _tkn = tkn;
        
        _newToken = 0;
        _myToken = 0;
        _ack = 0;
    }

    public async Task UdpListenAsync()
    {
        await Task.Yield();
        UdpClient _listener = new UdpClient(_port); 
        UdpClient _sender = new UdpClient();

        // Console.WriteLine($"Running listener: {Thread.CurrentThread.ManagedThreadId}");
        Random rng = new Random(_port);

        while(true)
        {
            try
            {
                byte[] buffer = new byte[64];
                await _listener.ReceiveAsync().ContinueWith(async (data) =>
                {
                    UdpReceiveResult datagram = data.Result;
                    string[] values = Encoding.ASCII.GetString(datagram.Buffer,0,datagram.Buffer.Length).Split(':');
                    Message msg = new Message(  int.Parse(values[0]), 
                                                int.Parse(values[1]), 
                                                (MsgType) Enum.Parse(typeof(MsgType), values[2]));
                    if(msg.Port != _port) 
                    {
                        //to nie jest wiadomosc do mnie, wysylam dalej
                        if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit)
                        {
                            await _sender.SendAsync(datagram.Buffer, 
                                                    datagram.Buffer.Length, 
                                                    new IPEndPoint(IPAddress.Loopback, _nextPort));
                        }       
                    }
                    else
                    {
                        if(msg.Type == MsgType.TOKEN)
                        {
                            if(_newToken < msg.Value && _myToken < msg.Value)
                            {
                                _newToken = msg.Value;
                            } 
                        }
                        else
                        {
                            if(_ack < msg.Value) _ack = msg.Value;
                        } 
                    }
                });
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            } 
        }
    }

    public async Task TokenRingAlgorithmAsync()
    {
        await Task.Yield();
        Random rng = new Random(_port);
        UdpClient _sender = new UdpClient();
        _sender.Connect(IPAddress.Loopback, _nextPort);

        if(_tkn == 1)
        {
            _myToken = 1;
            Thread.Sleep(5);
            Message msg = new Message(_nextPort, 1, MsgType.TOKEN);
            if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
            {
                var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                await _sender.SendAsync(buffer, buffer.Length);
                Console.WriteLine($"{_port}: Send TOKEN {_myToken}");
            }
            
        }
        while(true)
        {
            if(_myToken == 0 && _newToken == 0)
            {
                //nie mam tokenu oraz nie dostalem nowego, spimy dalej
                Thread.Sleep(5);
            }
            else
            {
                Thread.Sleep(StaticHelpers.Timeout);
                if(_ack == _myToken && _myToken != 0)
                {
                    // otrzymalem ACK na wyslanie tokenu, dotarl, moge usunac token z pamieci
                    Console.WriteLine($"{_port}: Get ACK for sent TOKEN {_myToken}");
                    _myToken = 0;
                }
                else if(_newToken > _myToken && _newToken > _ack) // && ((_myToken != 0 && tkn == 1) || tkn == 0))
                {
                    //odebrano nowy token
                    Console.WriteLine($"{_port}: Get new TOKEN {_newToken}");
                    //wyslij ACK odbioru
                    Message msg = new Message(_prevPort, _newToken, MsgType.ACK);
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                        await _sender.SendAsync(buffer, buffer.Length);
                        Console.WriteLine($"{_port}: Send ACK {_newToken}");
                    }
                    // utworz moj token
                    _myToken = _newToken + 1;
                    _newToken = 0;
                    //sumuluj dzialanie
                    Thread.Sleep(5);
                    //wyslij token dalej (skonczylem przetwarzac)
                    Message msg2 = new Message(_nextPort, _myToken, MsgType.TOKEN);
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                        await _sender.SendAsync(buffer, buffer.Length);
                        Console.WriteLine($"{_port}: Send TOKEN {_myToken}");
                    }
                }
                else if(_myToken != 0)
                { 
                    // nie dostalem nowego tokenu oraz nie dostalem ACK, wysylam ponownie
                    // druga opcja, dostalem stary zeton, ale mam _ack nowsze
                    //Console.WriteLine($"{port}: {_newToken}, {_myToken}, {_ack}");
                    
                    Message msg = new Message(_nextPort, _myToken, MsgType.TOKEN);
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                        await _sender.SendAsync(buffer, buffer.Length);
                        Console.WriteLine($"{_port}: Resend TOKEN {_myToken}");
                    }
                }
            }
        }
    }
}