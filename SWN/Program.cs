using System;
using System.Threading.Tasks;
using System.Collections.Generic;


Console.WriteLine("Creating processes");
List<ProcessService> processes = new List<ProcessService>{
    new ProcessService(50124, 50120, 50121, 1),
    new ProcessService(50120, 50121, 50122, 0),
    new ProcessService(50121, 50122, 50123, 0),
    new ProcessService(50122, 50123, 50124, 0),
    new ProcessService(50123, 50124, 50120, 0)
};

List<Task> tasks = new List<Task>();
foreach(var process in processes)
{
    tasks.Add(process.UdpListenAsync());  
    tasks.Add(process.TokenRingAlgorithmAsync()); 
}

Task.WaitAll(tasks.ToArray());
