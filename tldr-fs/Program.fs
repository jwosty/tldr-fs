﻿open Argu
open FSharp.Data
open System
open System.IO
open System.IO.Compression

module String =
    let join (separator: string) (strings: string seq) = String.Join (separator, strings)    

// TODO: support Windows
let homeDir = System.Environment.GetEnvironmentVariable "HOME"
let dataDir = Path.Combine [|homeDir; "tldr-fs"|]

let [<Literal>] indexJsonUri = "https://tldr.sh/assets/index.json"
let [<Literal>] pagesArchiveUri = "https://tldr.sh/assets/tldr.zip"

type Packages = JsonProvider<indexJsonUri>

let packageIndex = lazy (Packages.Load indexJsonUri)
let packageIndexMap = lazy (
    packageIndex.Force().Commands
    |> Seq.map (fun cmd -> cmd.Name.String.Value, cmd.Platform)
    |> Map.ofSeq)

let listAllPackages (packageIndexCommands: Packages.Command seq) =
    // for command in packageIndex.Force().Commands do
    for command in packageIndexCommands do
        match command.Name.String with
        | Some command -> printf "%s, " command
        | None -> printf "(bad json: '%A'), " command.Name.JsonValue
    Console.SetCursorPosition(Console.CursorLeft - 2, Console.CursorTop)
    printfn "  "

let listPackage package platform =
    use archiveStream = Http.RequestStream(pagesArchiveUri).ResponseStream
    use archive = new ZipArchive(archiveStream, ZipArchiveMode.Read)
    let pkEntry =
        archive.Entries |> Seq.tryPick (fun arc ->
            if Path.GetFileNameWithoutExtension arc.FullName = package then
                let entPlatform = Path.GetFileName (Path.GetDirectoryName arc.FullName)
                if platform = entPlatform then Some (arc, platform)
                else None
            else None)
    match pkEntry with
    | Some (pkEntry, platform) ->
        use mdStream = pkEntry.Open ()
        use sr = new StreamReader(mdStream)
        printfn "Found documentation for package '%s':\n%s" package (sr.ReadToEnd ())
    | None -> failwith "This page doesn't exist yet! Submit new pages here: https://github.com/tldr-pages/tldr"

module CLI =
    type CLIArgs =
        | [<MainCommand; Unique>] Package of string
        | Platform of string
        | List_All
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Package _ -> "display the tldr page for a package."
                | Platform _ -> "specify a platform to take into account when searching the packages; defaults to the current."
                | List_All -> "list all packages in the package database."

    let parser = ArgumentParser.Create<CLIArgs>(programName = "tldr-fs")

    [<EntryPoint>]
    let main args =
        try
            let args = parser.Parse args
            let pi = packageIndex.Force()
            if args.Contains List_All then listAllPackages pi.Commands
            let platform = args.GetResult <@ Platform @>
            args.TryGetResult <@ Package @> |> Option.iter (fun package -> listPackage package platform)
            
            0
        with
        | :? Argu.ArguParseException as e ->
            printfn "%s" (e.Message)
            1