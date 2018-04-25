open Argu
open FSharp.Data
open System

module String =
    let join (separator: string) (strings: string seq) = String.Join (separator, strings)    

let [<Literal>] indexJsonUri = "https://tldr.sh/assets/index.json"

type Packages = JsonProvider<indexJsonUri>

let packageIndex = lazy (Packages.Load indexJsonUri)

let listAllPackages () =
    for command in packageIndex.Force().Commands do
        match command.Name.String with
        | Some command -> printf "%s, " command
        | None -> printf "(bad json: '%A'), " command.Name.JsonValue
    Console.SetCursorPosition(Console.CursorLeft - 2, Console.CursorTop)
    printfn "  "

let listPackage package = printfn "listing package: %s" package

module CLI =
    type CLIArgs =
        | [<MainCommand; Unique>] Package of string
        | [<Unique>] List_All
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Package _ -> "display the tldr page for a package"
                | List_All -> "list all packages in the package database."

    let parser = ArgumentParser.Create<CLIArgs>(programName = "tldr-fs")

    [<EntryPoint>]
    let main args =
        try
            let args = parser.Parse args
            if args.Contains List_All then listAllPackages ()
            args.TryGetResult <@ Package @> |> Option.iter listPackage
            0
        with
        | :? Argu.ArguParseException as e ->
            printfn "%s" (e.Message)
            1