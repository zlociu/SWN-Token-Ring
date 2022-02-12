global using System;
using System.Threading.Tasks;
using System.Collections.Generic;

Console.WriteLine("Creating processes");
List<ProcessService> processes = new List<ProcessService>{
    ProcessService.Create(50120).AddPrevPort(50124).AddStartToken(),
    ProcessService.Create(50121),
    ProcessService.Create(50122),
    ProcessService.Create(50123),
    ProcessService.Create(50124).AddNextPort(50120)
};

List<Task> tasks = new();
foreach(var process in processes)
    tasks.AddRange(process.GetProcessTasks());  

Task.WaitAll(tasks.ToArray());
