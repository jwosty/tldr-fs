open Argu
open FSharp.Data
open System
open System.IO
open System.IO.Compression
open System.Runtime.InteropServices

exception GenericException of string
exception ArchiveException of string * exn

module String =
    let join (separator: string) (strings: string seq) = String.Join (separator, strings)    

// TODO: support Windows
let homeDir = System.Environment.GetEnvironmentVariable "HOME"
let dataDir = Path.Combine [|homeDir; "tldr-fs"|]
let localIndex = Path.Combine [|dataDir; "pages/index.json"|]
let pagesDir = Path.Combine [|dataDir; "pages"|]

let [<Literal>] indexJsonUri = "https://tldr.sh/assets/index.json"
let [<Literal>] pagesArchiveUri = "https://tldr.sh/assets/tldr.zip"

module PackageInfo =
    type Packages = JsonProvider<indexJsonUri>

    let packageIndex = lazy (
        try
            let json = Packages.Load indexJsonUri
            Directory.CreateDirectory (Path.GetDirectoryName localIndex) |> ignore
            use file = File.OpenWrite localIndex
            use writer = new StreamWriter(file)
            json.JsonValue.WriteTo (writer, JsonSaveOptions.None)
            json
        with :? System.Net.WebException ->
            Packages.Load ((new Uri(localIndex)).AbsoluteUri)
        )
    let packageIndexMap = lazy (
        packageIndex.Force().Commands
        |> Seq.map (fun cmd -> cmd.Name.String.Value, cmd.Platform)
        |> Map.ofSeq)

    let listAllPackages (packageIndexCommands: Packages.Command seq) =
        for command in packageIndexCommands do
            match command.Name.String with
            | Some command -> printf "%s, " command
            | None -> printf "(bad json: '%A'), " command.Name.JsonValue
        Console.SetCursorPosition(Console.CursorLeft - 2, Console.CursorTop)
        printfn "  "

    let fetchPagesForPackage package =
        Directory.EnumerateDirectories pagesDir
        // it's GetFileName not GetDirectoryName because the latter only works when there's an ending slash
        |> Seq.map (fun dir -> Path.GetFileName dir, Path.Combine [|dir; package + ".md"|])
        |> Seq.choose (fun (platform, path) -> if File.Exists path then Some (platform, fun () -> File.ReadAllText path) else None)

    let printPackagePage package preferredPlatform =
        let pages = fetchPagesForPackage package
        let page = pages |> Seq.sortBy (function (pl, _) when pl = preferredPlatform -> 0 | ("common", _) -> 1 | _ -> 2) |> Seq.tryHead
        match page with
        | Some (platform, getText) -> printfn "Found '%s' page (%s):\n%s" package platform (getText ())
        | None -> raise (GenericException "This page doesn't exist yet! Submit new pages here: https://github.com/tldr-pages/tldr")

module CLI =
    type CLIArgs =
        | [<MainCommand; Unique>] Package of string
        | [<AltCommandLine "-p">] Platform of string
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
            let currentPlatform =
                [OSPlatform.Linux, "linux"; OSPlatform.OSX, "osx"; OSPlatform.Windows, "windows"]
                |> List.tryPick (fun (pl, plName) -> if RuntimeInformation.IsOSPlatform pl then Some plName else None)

            let args = parser.Parse args
            let pi = PackageInfo.packageIndex.Force()
            if args.Contains List_All then PackageInfo.listAllPackages pi.Commands
            let platform =
                match args.TryGetResult <@ Platform @>, currentPlatform with
                | Some pl, _ | _, Some pl -> pl
                | None, None -> raise (GenericException "Failed to detect platform and no explicit platform was given.")
            args.TryGetResult <@ Package @> |> Option.iter (fun package -> PackageInfo.printPackagePage package platform)

            0
        with
        | GenericException msg ->
            System.Console.Error.WriteLine msg
            1
        | :? Argu.ArguParseException as e ->
            printfn "%s" (e.Message)
            1