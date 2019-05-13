// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

#I @"../../../.paket/load/net471"
#load "SSH.NET.fsx"

#r @"../Model/bin/Debug/Yang.Model.dll"

#load "Client.fs"
#load "Capability.fs"

open System
open Renci.SshNet
open Yang.NetConf

// Define your library scripting code here

let machine = Environment.GetEnvironmentVariable("NETCONF_TEST_MACHINE")
let port =
    let from_environment = Environment.GetEnvironmentVariable("NETCONF_TEST_PORT")
    if String.IsNullOrWhiteSpace(from_environment) then 830
    else Int32.Parse(from_environment)
let user = Environment.GetEnvironmentVariable("NETCONF_TEST_USER")
let password = Environment.GetEnvironmentVariable("NETCONF_TEST_PASSWORD")

let client = new NetConfClient(machine, port, user, password)

let ConnectAsync (client : NetConfClient) = async { client.Connect() }

Async.RunSynchronously(ConnectAsync client)
assert(client.IsConnected)

let example = """
<rpc message-id="205" xmlns="urn:ietf:params:xml:ns:netconf:base:1.1">
    <get-config>
        <source>
            <running/>
        </source>
    </get-config>
</rpc>
"""

let request = new Xml.XmlDocument()
let item = request.AppendChild(request.CreateElement("get-config"))
let source = item.AppendChild(request.CreateElement("source"))
let running = source.AppendChild(request.CreateElement("running"))
request.OuterXml

client.IsConnected
client.AutomaticMessageIdHandling <- false
let r2 = client.SendReceiveRpc(request)
let response = client.SendReceiveRpc(example)

let GetSessionId (document : Xml.XmlDocument) =
    ((document.Item "hello").Item "session-id").InnerText |> Int32.Parse

let session_id = GetSessionId client.ServerCapabilities
printfn "Session id: %d" session_id

let capabilities = Capability.ReadCapabilities client.ServerCapabilities
let client_capabilities = Capability.ReadCapabilities client.ClientCapabilities
capabilities |> List.iter (printfn "%A")

let uri = Uri("urn:ietf:params:xml:ns:netconf:base:1.0?module=ietf-netconf&revision=2011-06-01")
let version = Capability.TryGetVersion (uri)
assert(version.IsSome)
assert(Version(1, 0) = version.Value)

let cc = capabilities |> List.map Capability.Capability.Make
cc |> List.map (fun cap -> cap.Name.OriginalString, cap.Version) |> List.iter (printfn "%A")
cc |> List.map (fun cap -> cap.Name.OriginalString, cap.Revision) |> List.iter (printfn "%A")
cc |> List.map (fun cap -> cap.Name.OriginalString, cap.NetConfCapabilityName) |> List.iter (printfn "%A")

cc |> List.choose (Capability.StandardCapabilitity.TryMake)

client.Dispose()
