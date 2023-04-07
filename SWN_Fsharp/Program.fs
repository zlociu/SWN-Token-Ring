// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System.Threading.Tasks
open System.Collections.Generic

open ProcessService

printfn "Creating processes"
let processes = new List<ProcessService>()
let tasks = new List<Task>()

let ports = [   (50124, 50120, 50121); 
                (50120, 50121, 50122); 
                (50121, 50122, 50123); 
                (50122, 50123, 50124); 
                (50123, 50124, 50120)]

for elem in ports do
    new ProcessService(elem) |> processes.Add 

processes[0].AddStartToken() |> ignore

for proc in processes do
    proc.UdpListenAsync() |> Async.StartAsTask |> tasks.Add 
    proc.TokenRingAlgorithmAsync() |> Async.StartAsTask |> tasks.Add 

tasks.ToArray() |> Task.WaitAny |> ignore
        