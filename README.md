# SWN-Token-Ring
An implementation of algorithm of token transport in unreliable token-ring topology.  
Implementation in C# & F# in .NET 6 using async [Tasks](https://docs.microsoft.com/pl-pl/dotnet/api/system.threading.tasks.task?view=net-6.0.).  
Works on Windows & Linux (tested on Windows 10 & Fedora 35).

### How to run 
#### C# 
1. Go to folder where file ___.csproj___ exists.
2. Open cmd/terminal.
3. Type: `dotnet run`

#### F# 
1. Go to folder where file ___.fsproj___ exists.
2. Open cmd/terminal.
3. Type: `dotnet run`

### How algorithm works?
0. Initial conditions:
   - synchronous workflow 
   - NO FIFO in communication (token value increase infinitely) 
   - all messages are sent in one direction
   - communication channels can be unreliable but in finite time.
1. Only one process/thread which have token can do things (critical section).
2. Process (1) send token to next process. (e.g. 1 -> 2)
3. Process (2) get token, sends ack with token number.
4. Process (2) do things in its critical section and then sends token again (with bigger sequencial value e.g. +1).
    1. If process (1) didn't received ack for sent token in specified time (timeout), resend it.
    2. Process (1) will resending token until:
       - get ack OR
       - get newer token than it has.


### Addition
Project was created for High Reliability Systems Lab course held by Institute of Computing Science, Poznań University of Technology.
