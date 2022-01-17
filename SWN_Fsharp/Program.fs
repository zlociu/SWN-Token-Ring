// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System.Threading.Tasks
open System.Collections.Generic

open ProcessService

printfn "Creating processes"
let processes = new List<ProcessService>()
let tasks = new List<Task>()

let ports = [   (50124, 50120, 50121, true); 
                (50120, 50121, 50122, false); 
                (50121, 50122, 50123, false); 
                (50122, 50123, 50124, false); 
                (50123, 50124, 50120, false)]

for elem in ports do
    new ProcessService(elem) |> processes.Add 

for proc in processes do
    Task.Run( fun () -> proc.UdpListenAsync()) |> tasks.Add 
    Task.Run( fun () -> proc.TokenRingAlgorithmAsync()) |> tasks.Add 

let _ = tasks.ToArray() |> Task.WaitAny
        