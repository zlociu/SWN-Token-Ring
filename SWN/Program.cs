global using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (sender, args) => {
    Console.WriteLine("Canceling...");
    cts.Cancel();
};

Console.WriteLine("Creating processes");
List<ProcessService> processes = new() {
    ProcessService.Create(50120).AddPrevPort(50124).AddStartToken(),
    ProcessService.Create(50121),
    ProcessService.Create(50122),
    ProcessService.Create(50123),
    ProcessService.Create(50124).AddNextPort(50120)
};

var tasks = processes
    .SelectMany(process => process.GetProcessTasks(cts.Token))
    .ToArray(); 

Task.WaitAll(tasks, cts.Token);
