using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

public class ProcessService
{
    private int _newToken;
    private int _myToken;
    private int _ack;
    private int _prevPort; 
    private int _port; 
    private int _nextPort;
    private bool _tkn;

    public int Port => _port;

    private ProcessService(int port)
    {
        _prevPort = port - 1;
        _port = port;
        _nextPort = port + 1;
        _tkn = false;
        
        _newToken = 0;
        _myToken = 0;
        _ack = 0;
    }

    ///<summary>
    ///Create new ProcessService object with specified port. <para></para>
    ///Prev port is 1 smaller , next is 1 bigger, have NO token
    ///</summary>
    public static ProcessService Create(int port)
    {
        return new ProcessService(port);
    }

    ///<summary>
    ///Set other than default next port. <para></para>
    ///</summary>
    public ProcessService AddNextPort(int port)
    {
        this._nextPort = port;
        return this;
    }

    ///<summary>
    ///Set other than default previous port. <para></para>
    ///</summary>
    public ProcessService AddPrevPort(int port)
    {
        this._prevPort = port;
        return this;
    }

    ///<summary>
    ///Add start token to process (should be set only to one process) <para></para>
    ///</summary>
    public ProcessService AddStartToken()
    {
        this._tkn = true;
        return this;
    }

    public async Task UdpListenAsync(CancellationToken cancellationToken)
    {
        UdpClient _listener = new UdpClient(_port); 
        UdpClient _sender = new UdpClient();
        Random rng = new Random(_port);

        while(cancellationToken.IsCancellationRequested == false)
        {
            try
            {
                byte[] buffer = new byte[64];
                await _listener.ReceiveAsync().ContinueWith(async (data) =>
                {
                    UdpReceiveResult datagram = data.Result;
                    string[] values = Encoding.ASCII.GetString(datagram.Buffer,0,datagram.Buffer.Length).Split(':');
                    Message msg = new(  Port: int.Parse(values[0]), 
                                        Value: int.Parse(values[1]), 
                                        Type: Enum.Parse<MsgType>(values[2]));
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
                            if(_newToken < msg.Value && _myToken < msg.Value) _newToken = msg.Value;
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

    public async Task TokenRingAlgorithmAsync(CancellationToken cancellationToken)
    {
        Random rng = new(_port);
        UdpClient _sender = new();
        _sender.Connect(IPAddress.Loopback, _nextPort);

        if(_tkn)
        {
            _myToken = 1;
            await Task.Delay(5);
            
            if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
            {
                Message msg = new(_nextPort, 1, MsgType.TOKEN);
                var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                await _sender.SendAsync(buffer, buffer.Length);
                Console.WriteLine($"{_port}: Send TOKEN {_myToken}");
            }
            
        }

        while(cancellationToken.IsCancellationRequested == false)
        {
            if(_myToken == 0 && _newToken == 0)
            {
                //nie mam tokenu oraz nie dostalem nowego, spimy dalej
                await Task.Delay(5);
            }
            else
            {
                await Task.Delay(StaticHelpers.Timeout);
                if(_ack == _myToken && _myToken != 0)
                {
                    // otrzymalem ACK na wyslanie tokenu, dotarl, moge usunac token z pamieci
                    Console.WriteLine($"{_port}: Get ACK for sent TOKEN {_myToken}");
                    _myToken = 0;
                }
                else if(_newToken > _myToken && _newToken > _ack)
                {
                    //odebrano nowy token
                    Console.WriteLine($"{_port}: Get new TOKEN {_newToken}");
                    //wyslij ACK odbioru
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        Message msg = new(_prevPort, _newToken, MsgType.ACK);
                        var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                        await _sender.SendAsync(buffer, buffer.Length);
                        Console.WriteLine($"{_port}: Send ACK {_newToken}");
                    }
                    // utworz moj token
                    _myToken = _newToken + 1;
                    _newToken = 0;
                    //symuluj dzialanie
                    await Task.Delay(5);
                    //wyslij token dalej (skonczylem przetwarzac)
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        Message msg = new(_nextPort, _myToken, MsgType.TOKEN);
                        var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                        await _sender.SendAsync(buffer, buffer.Length);
                        Console.WriteLine($"{_port}: Send TOKEN {_myToken}");
                    }
                }
                else if(_myToken != 0)
                { 
                    // nie dostalem nowego tokenu oraz nie dostalem ACK, wysylam ponownie
                    // druga opcja, dostalem stary zeton, ale mam _ack nowsze
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        Message msg = new(_nextPort, _myToken, MsgType.TOKEN);
                        var buffer = Encoding.ASCII.GetBytes(msg.ToString());
                        await _sender.SendAsync(buffer, buffer.Length);
                        Console.WriteLine($"{_port}: Resend TOKEN {_myToken}");
                    }
                }
            }
        }
    }

    public IEnumerable<Task> GetProcessTasks(CancellationToken token)
    {
        return new Task[]{
            UdpListenAsync(token),
            TokenRingAlgorithmAsync(token)
        };
    }
}