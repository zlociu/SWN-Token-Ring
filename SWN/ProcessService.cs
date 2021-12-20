using System;
using System.Collections.Generic;
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
    public int PrevPort {get;}
    public int Port {get;}
    public int NextPort {get;}
    public int Tkn {get;}

    public ProcessService(int prevPort, int port, int nextPort, int tkn)
    {
        PrevPort = prevPort;
        Port = port;
        NextPort = nextPort;
        Tkn = tkn;
        
        _newToken = 0;
        _myToken = 0;
        _ack = 0;
    }

    public async Task tcpListen(int port, int nextPort)
    {
        await Task.Yield();
        TcpListener _listener = new TcpListener(IPAddress.IPv6Loopback, port); 
        _listener.Start();

        // Console.WriteLine($"Running listener: {Thread.CurrentThread.ManagedThreadId}");
        Random rng = new Random(port);
        int iter = 0;

        // Console.WriteLine(_sender.Connected);

        while(true)
        {
            iter++;
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                byte[] buffer = new byte[64];
                await client.GetStream().ReadAsync(buffer, 0, 64).ContinueWith(async (count) =>
                {
                    int len = count.Result;
                    // while(true)
                    // {
                        string[] values = Encoding.ASCII.GetString(buffer,0,len).Split(':');
                        Message msg = new Message(  int.Parse(values[0]), 
                                                    int.Parse(values[1]), 
                                                    (MsgType) Enum.Parse(typeof(MsgType), values[2]));
                        if(msg.Port != port) 
                        {
                            //to nie jest wiadomosc do mnie, wysylam dalej
                            if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit)
                            {
                                TcpClient _sender = new TcpClient();
                                await _sender.ConnectAsync(IPAddress.IPv6Loopback, nextPort);
                                await _sender.GetStream().WriteAsync(buffer, 0, len);
                                _sender.GetStream().Close();
                                _sender.Close();
                            }       
                        }
                        else
                        {
                            if(msg.Type == MsgType.TOKEN)
                            {
                                if(_newToken < msg.Value && _myToken < msg.Value)
                                {
                                    _newToken = msg.Value;
                                    //Console.WriteLine($"{port}: Get newer TOKEN {_newToken}");
                                } 
                            }
                            else
                            {
                                if(_ack < msg.Value) _ack = msg.Value;
                            } 
                        }
                        // len = await client.GetStream().ReadAsync(buffer, 0, 64);
                    // }
                });
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            } 
        }
    }

    public async Task tokenRingAlgorithm(int prevPort, int port, int nextPort, int tkn)
    {
        await Task.Yield();
        //Console.WriteLine($"Running token: {Thread.CurrentThread.ManagedThreadId}");
        Random rng = new Random(port);
    
        if(tkn == 1)
        {
            _myToken = 1;
            Thread.Sleep(5);
            Message msg = new Message(nextPort, 1, MsgType.TOKEN);
            if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
            {
                TcpClient _sender = new TcpClient();
                await _sender.ConnectAsync(IPAddress.IPv6Loopback, nextPort);
                await _sender.GetStream().WriteAsync(Encoding.ASCII.GetBytes(msg.ToString()));
                Console.WriteLine($"{port}: Send TOKEN {_myToken}");
                _sender.GetStream().Close();
                _sender.Close();
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
                TcpClient _sender = new TcpClient();
                await _sender.ConnectAsync(IPAddress.IPv6Loopback, nextPort);

                Thread.Sleep(StaticHelpers.Timeout);
                if(_ack == _myToken && _myToken != 0)
                {
                    // otrzymalem ACK na wyslanie tokenu, dotarl, moge usunac token z pamieci
                    Console.WriteLine($"{port}: Get ACK for sent TOKEN {_myToken}");
                    _myToken = 0;
                }
                else if(_newToken > _myToken && _newToken > _ack) // && ((_myToken != 0 && tkn == 1) || tkn == 0))
                {
                    //odebrano nowy token
                    Console.WriteLine($"{port}: Get new TOKEN {_newToken}");
                    //wyslij ACK odbioru
                    Message msg = new Message(prevPort, _newToken, MsgType.ACK);
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        await _sender.GetStream().WriteAsync(Encoding.ASCII.GetBytes(msg.ToString()));
                        Console.WriteLine($"{port}: Send ACK {_newToken}");
                    }
                    // utworz moj token
                    _myToken = _newToken + 1;
                    _newToken = 0;
                    //sumuluj dzialanie
                    Thread.Sleep(5);
                    //wyslij token dalej (skonczylem przetwarzac)
                    Message msg2 = new Message(nextPort, _myToken, MsgType.TOKEN);
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        await _sender.GetStream().WriteAsync(Encoding.ASCII.GetBytes(msg2.ToString()));
                        Console.WriteLine($"{port}: Send TOKEN {_myToken}");
                    }
                }
                else if(_myToken != 0)
                { 
                    // nie dostalem nowego tokenu oraz nie dostalem ACK, wysylam ponownie
                    // druga opcja, dostalem stary zeton, ale mam _ack nowsze
                    //Console.WriteLine($"{port}: {_newToken}, {_myToken}, {_ack}");
                    
                    Message msg = new Message(nextPort, _myToken, MsgType.TOKEN);
                    if(rng.NextDouble() > StaticHelpers.BreakConnectionLimit) 
                    {
                        await _sender.GetStream().WriteAsync(Encoding.ASCII.GetBytes(msg.ToString()));
                        Console.WriteLine($"{port}: Resend TOKEN {_myToken}");
                    }
                }

                _sender.GetStream().Close();
                _sender.Close();
            }
        }
    }
}