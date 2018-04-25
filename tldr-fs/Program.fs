open Argu
open FSharp.Data
open System
open System.IO
open System.IO.Compression
open System.Runtime.InteropServices

exception GenericException of string

module String =
    let join (separator: string) (strings: string seq) = String.Join (separator, strings)    

// TODO: support Windows
let homeDir = System.Environment.GetEnvironmentVariable "HOME"
let dataDir = Path.Combine [|homeDir; "tldr-fs"|]

let [<Literal>] indexJsonUri = "https://tldr.sh/assets/index.json"
let [<Literal>] pagesArchiveUri = "https://tldr.sh/assets/tldr.zip"

module PackageInfo =
    type Packages = JsonProvider<indexJsonUri>

    let packageIndex = lazy (Packages.Load indexJsonUri)
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

    let fetchPackageMarkdown package preferredPlatform =
        use archiveStream = Http.RequestStream(pagesArchiveUri).ResponseStream
        use archive = new ZipArchive(archiveStream, ZipArchiveMode.Read)
        archive.Entries |> Seq.tryPick (fun arc ->
            if Path.GetFileNameWithoutExtension arc.FullName = package then
                let entPlatform = Path.GetFileName (Path.GetDirectoryName arc.FullName)
                if preferredPlatform = entPlatform then Some (arc, preferredPlatform)
                else None
            else None)
        |> Option.map (fun (pkEntry, platform) ->
            use mdStream = pkEntry.Open ()
            use sr = new StreamReader(mdStream)
            platform, sr.ReadToEnd ())

    let printPackagePage package preferredPlatform =
        match fetchPackageMarkdown package preferredPlatform with
        | Some (platform, md) ->
            printfn "Found documentation for package '%s' for '%s':\n%s" package platform md
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
            let pi = packageIndex.Force()
            if args.Contains List_All then listAllPackages pi.Commands
            let platform =
                match args.TryGetResult <@ Platform @>, currentPlatform with
                | Some pl, _ | _, Some pl -> pl
                | None, None -> raise (GenericException "Failed to detect platform and no explicit platform was given.")
            args.TryGetResult <@ Package @> |> Option.iter (fun package -> listPackage package platform)

            0
        with
        | GenericException msg ->
            System.Console.Error.WriteLine msg
            1
        | :? Argu.ArguParseException as e ->
            printfn "%s" (e.Message)
            1