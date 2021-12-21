// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Net
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

open ProcessService

[<EntryPoint>]
let main argv =
    printfn "Creating processes"
    let processes = new List<ProcessService>()

    let ports = [   (50124, 50120, 50121, true); 
                    (50120, 50121, 50122, false); 
                    (50121, 50122, 50123, false); 
                    (50122, 50123, 50124, false); 
                    (50123, 50124, 50120, false)]
    
    for elem in ports do
        new ProcessService(elem) |> processes.Add 

    let tasks = new List<Task>()
    for proc in processes do
        tasks.Add (proc.udpListen proc.Port proc.NextPort |> Async.StartAsTask)
        tasks.Add (proc.tokenRingAlgorithm proc.PrevPort  proc.Port  proc.NextPort  proc.Tkn |> Async.StartAsTask)
    
    tasks.ToArray() |> Task.WaitAny 
        
