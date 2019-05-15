// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open System.IO
open Yang.Model
open Yang.Parser
open Yang.Parser.Module

let fail_with_help () =
    Printf.eprintfn "Error in command"
    Environment.Exit(-1)
    failwith "This point should not be reached"

let validate arguments =
    match arguments with
    | []    ->
        Printf.eprintfn "Validate takes at least one argument"
        fail_with_help ()
    | model :: _ ->
        try
            match Parser.ParseFile model with
            | ModelUnit.ModuleUnit m ->
                printfn "Detected module: %s" m.Name.Value
            | ModelUnit.SubmoduleUnit m ->
                printfn "Detected submodule: %s" m.Name.Value

        with
        | :? YangParserException ->
            Printf.eprintfn "Error in parsing model"
            Environment.Exit(-1)
            ()

let download_config arguments =
    raise (new NotImplementedException())

let private ieq (str1:string) (str2:string) =
    if Object.ReferenceEquals(str1, str2) then true
    elif Object.ReferenceEquals(null, str1) || Object.ReferenceEquals(null, str2) then false
    else str1.Equals(str2, StringComparison.InvariantCultureIgnoreCase)

[<EntryPoint>]
let main argv =
    Parser.Initialize()

    let all_args = Environment.GetCommandLineArgs()
    let command = all_args.[0]
    let file_with_extension = FileInfo(command)
    let file = file_with_extension.Name.Replace(file_with_extension.Extension, "")

    let operation, args =
        if file.Equals("nyang", StringComparison.InvariantCultureIgnoreCase) then
            if argv.Length = 0 then
                fail_with_help ()
            else
                argv.[0], (Array.toList argv |> List.tail)

        else file, (Array.toList argv)

    if ieq operation "validate" then
        validate args
    elif ieq operation "getconfig" then
        download_config args
    else
        Printf.eprintfn "Unknown operation: %s" operation
        fail_with_help ()

    0 // return an integer exit code
