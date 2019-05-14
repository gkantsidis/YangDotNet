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

let StoreKey (client : NetConfClient) =
    let host = client.ConnectionInfo.Host
    let port = client.ConnectionInfo.Port
    let filename = sprintf "%s_%d.pem" host port
    let path =
        let keylogfile = Environment.GetEnvironmentVariable("SSLKEYLOGFILE")
        if String.IsNullOrWhiteSpace(keylogfile) then
            printfn "Storing key uner TEMP/keys directory"
            let keysdir = IO.Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "keys")
            if IO.Directory.Exists(keysdir) = false then
                printfn "Creating TEMP/keys directory"
                IO.Directory.CreateDirectory(keysdir) |> ignore
            keysdir
        else
            let info = IO.DirectoryInfo(keylogfile)
            IO.Path.Combine(info.Parent.FullName)
    let fullname = IO.Path.Combine(path, filename)

    let write (key : Common.HostKeyEventArgs) =
        let sb = Text.StringBuilder()
        Printf.bprintf sb "----BEGIN RSA PRIVATE KEY-----\n"
        let bytes = Convert.ToBase64String(key.FingerPrint)
        Printf.bprintf sb "%s\n" bytes
        Printf.bprintf sb "----END RSA PRIVATE KEY-----"
        let content = sb.ToString()

        printfn "%s" content
        IO.File.WriteAllText(fullname, content)

    client.HostKeyReceived.Subscribe write |> ignore
    printfn "Writing to %s" fullname

let machine = Environment.GetEnvironmentVariable("NETCONF_TEST_MACHINE")
let port =
    let from_environment = Environment.GetEnvironmentVariable("NETCONF_TEST_PORT")
    if String.IsNullOrWhiteSpace(from_environment) then 830
    else Int32.Parse(from_environment)
let user = Environment.GetEnvironmentVariable("NETCONF_TEST_USER")
let password = Environment.GetEnvironmentVariable("NETCONF_TEST_PASSWORD")

let client = new NetConfClient(machine, port, user, password)

StoreKey client

let ConnectAsync (client : NetConfClient) = async { client.Connect() }

Async.RunSynchronously(ConnectAsync client)
assert(client.IsConnected)

let rpc = new Xml.XmlDocument()
let element = rpc.CreateElement("rpc", namespaceURI="urn:ietf:params:xml:ns:netconf:base:1.0")
let message_id = rpc.CreateAttribute("message-id")
message_id.Value <- "1"
element.Attributes.Append(message_id)
let request = rpc.AppendChild(element)
let item = request.AppendChild(rpc.CreateElement("get-config"))
let source = item.AppendChild(rpc.CreateElement("source"))
let running = source.AppendChild(rpc.CreateElement("running"))
request.OuterXml

client.OperationTimeout

try
    let _response = client.SendReceiveRpc(rpc)
    printfn "%A" _response
with
| :? Renci.SshNet.Common.SshConnectionException as e ->
    printfn "%A" e
    printfn "Inner: %A" e.InnerException
    printfn "Data: %A" e.Data
    printfn "Source: %A" e.Source
    printfn "Trace: %A" e.StackTrace

client.ConnectionInfo
client.HostKeyReceived.Subscribe(fun v -> printfn "Name: %s, fingerprint: %A, Key: %A" v.HostKeyName v.FingerPrint v.HostKey)

let GetSessionId (document : Xml.XmlDocument) =
    ((document.Item "hello").Item "session-id").InnerText |> Int32.Parse

let session_id = GetSessionId client.ServerCapabilities
printfn "Session id: %d" session_id

let capabilities = Capability.ReadCapabilities client.ServerCapabilities
let client_capabilities = Capability.ReadCapabilities client.ClientCapabilities
capabilities |> List.iter (printfn "%A")

let cc = capabilities |> List.map Capability.Capability.Make
cc |> List.map (fun cap -> cap.Name.OriginalString, cap.Version) |> List.iter (printfn "%A")
cc |> List.map (fun cap -> cap.Name.OriginalString, cap.Revision) |> List.iter (printfn "%A")
cc |> List.map (fun cap -> cap.Name.OriginalString, cap.NetConfCapabilityName) |> List.iter (printfn "%A")

cc |> List.choose (Capability.StandardCapabilitity.TryMake)

client.Dispose()
