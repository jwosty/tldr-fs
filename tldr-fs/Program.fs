open Argu
open FSharp.Data
open System

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

type CLIArgs =
    | [<Unique>] List_All
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | List_All -> "list all packages in the package database."

let parser = ArgumentParser.Create<CLIArgs>(programName = "tldr-fs")

[<EntryPoint>]
let main args =
    let args = parser.Parse args
    if args.Contains List_All then
        Async.RunSynchronously listAllPackages
    0