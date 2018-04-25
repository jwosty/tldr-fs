open FSharp.Data
open System
open System.Data

module String =
    let join (separator: string) (strings: string seq) =
        String.Join (separator, strings)    

type Packages = JsonProvider<"https://tldr.sh/assets/index.json">

let listAllPackages = async {
    let! packages = Packages.AsyncGetSample()
    for command in packages.Commands do
        match command.Name.String with
        | Some command -> printf "%s, " command
        | None -> printf "(unknown value: '%A'), " command.Name.JsonValue
    Console.SetCursorPosition(Console.CursorLeft - 2, Console.CursorTop)
    printfn "  " }

[<EntryPoint>]
let main _ =
    Async.RunSynchronously listAllPackages
    0