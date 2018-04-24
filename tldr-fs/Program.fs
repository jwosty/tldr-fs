// Learn more about F# at http://fsharp.org

open FSharp.Data
open System

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"
    //let index = Http.AsyncRequestString "http://tldr.sh/assets/index.json" |> Async.RunSynchronously
    let index = JsonValue.Parse "[1,2,3]"
    printfn "%A" index
    0