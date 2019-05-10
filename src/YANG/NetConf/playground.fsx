// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

#I @"../../../.paket/load/net471"
#load "SSH.NET.fsx"

#r @"../Model/bin/Debug/Yang.Model.dll"

#load "Client.fs"

open System
open Yang.Model
open Yang.NetConf

// Define your library scripting code here

let machine = Environment.GetEnvironmentVariable("NETCONF_TEST_MACHINE")
let port =
    let from_environment = Environment.GetEnvironmentVariable("NETCONF_TEST_PORT")
    if String.IsNullOrWhiteSpace(from_environment) then 830
    else Int32.Parse(from_environment)
let user = Environment.GetEnvironmentVariable("NETCONF_TEST_USER")
let password = Environment.GetEnvironmentVariable("NETCONF_TEST_PASSWORD")

let client = new Renci.SshNet.NetConfClient(machine, port, user, password)
client.Connect()
client.ClientCapabilities

let xx = client.ServerCapabilities
let session_id = ((xx.Item "hello").Item "session-id").InnerText |> Int32.Parse
let capabilities =
    let raw = ((xx.Item "hello").Item "capabilities").ChildNodes
    List.init raw.Count (fun i -> raw.Item i)
    |> List.map (
        fun capability ->
            assert (capability.LocalName.Equals("capability", StringComparison.InvariantCultureIgnoreCase))
            assert (capability.ChildNodes.Count = 1)
            assert (capability.FirstChild.LocalName.Equals("#text", StringComparison.InvariantCultureIgnoreCase))
            capability.FirstChild.Value
    )

capabilities |> List.iter (printfn "%A")

let parse_date (date : string) : Arguments.Date option =
    let pattern = "(?<Year>[1-2][0-9]{3})\-(?<Month>(0[1-9]|1[0-2]))\-(?<Day>([0-2][0-9]|3[0-1]))"
    let re = Text.RegularExpressions.Regex(pattern, Text.RegularExpressions.RegexOptions.Compiled)
    if re.IsMatch(date) then
        let result = re.Match(date)
        let year = result.Groups.Item("Year").Value |> Int32.Parse
        let month = result.Groups.Item("Month").Value |> Int32.Parse
        let day = result.Groups.Item("Day").Value |> Int32.Parse
        let date = Arguments.Date.Make(year, month, day)
        Some date
    else None

parse_date "2010-10-04"
parse_date "2010-01-04"
parse_date "2010-01-30"

parse_date "2010-13-04"
parse_date "2010-00-04"
parse_date "2010-32-04"
parse_date "2010-01-32"

type Capability = {
    Name        : string
    Prefix      : string
    Options     : Map<string, string>
    Revision    : Arguments.Date option
}
with
    static member Make (name : string) =
        assert (System.String.IsNullOrWhiteSpace(name) = false)
        let index = name.IndexOf('?')
        if index < 0 then
            {
                Name        = name
                Prefix      = name
                Options     = Map.empty
                Revision    = None
            }
        else
            let suffix =name.Substring(index+1)
            let options =
                suffix.Split('&')
                |> Seq.map(
                    fun option ->
                        let index = option.IndexOf('=')
                        assert(index > 0)
                        let name = option.Substring(0, index)
                        let value = option.Substring(index+1)
                        name, value
                )
                |> Map.ofSeq

            let revision =
                match options.TryFind "revision" with
                | None      -> None
                | Some rev  ->

            {
                Name    = name
                Prefix  = name.Substring(0, index)
                Options = options
            }

capabilities |> List.map Capability.Make

client.Dispose()
