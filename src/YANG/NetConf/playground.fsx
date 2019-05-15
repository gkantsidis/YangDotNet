// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

#I @"../../../.paket/load/net471"

#r @"../Model/bin/Debug/Yang.Model.dll"
#r @"../External/Renci.SshNet/bin/Debug/Renci.SshNet.dll"

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

open System.IO
open System.Text
open System.Xml

let ConnectAsync (client : NetConfClient) = async { client.Connect() }

type Configuration =
    static member GetCommand (?configuration_store: string, ?message_id: int) =
        let configuration_store = defaultArg configuration_store "running"
        let message_id = defaultArg message_id 1

        let rpc = new XmlDocument()
        let element = rpc.CreateElement("rpc", "urn:ietf:params:xml:ns:netconf:base:1.0")
        let messageId = rpc.CreateAttribute("message-id")
        // This value will get replaced
        messageId.Value <- message_id.ToString()
        element.Attributes.Append(messageId) |> ignore

        let request = rpc.AppendChild(element);
        let item = request.AppendChild(rpc.CreateElement("get-config"));
        let source = item.AppendChild(rpc.CreateElement("source"));
        source.AppendChild(rpc.CreateElement(configuration_store)) |> ignore
        rpc

    static member DownloadAsync (client: NetConfClient, ?configuration_store: string) =
        let configuration_store = defaultArg configuration_store "running"
        let request = Configuration.GetCommand (configuration_store)

        async {
            if client.IsConnected = false then
                do! ConnectAsync client
            assert(client.IsConnected)
            let response = client.SendReceiveRpc(request)
            return response
        }

    static member Download (client: NetConfClient, ?configuration_store: string) =
        let configuration_store = defaultArg configuration_store "running"
        Async.RunSynchronously (Configuration.DownloadAsync (client, configuration_store))

    static member DownloadAsStringAsync (client: NetConfClient, ?configuration_store: string) =
        let configuration_store = defaultArg configuration_store "running"
        async {
            let! configuration = Configuration.DownloadAsync (client, configuration_store)
            use mStream = new MemoryStream()
            use writer = new XmlTextWriter(mStream, Encoding.Unicode)
            writer.Formatting <- Formatting.Indented
            configuration.WriteContentTo(writer)
            writer.Flush()
            do! (mStream.FlushAsync() |> Async.AwaitTask)
            mStream.Position <- 0L
            use reader = new StreamReader(mStream)
            let formatted = reader.ReadToEnd()
            return formatted
        }

    static member DownloadAsString (client: NetConfClient, ?configuration_store: string) =
        let configuration_store = defaultArg configuration_store "running"
        Async.RunSynchronously (Configuration.DownloadAsStringAsync (client, configuration_store))

let configuration = Configuration.DownloadAsString client


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


let GetSchemaCommand (urn : string, name : string) =
    let rpc = new XmlDocument()
    let element = rpc.CreateElement("rpc", "urn:ietf:params:xml:ns:netconf:base:1.0")
    let messageId = rpc.CreateAttribute("message-id")
    // This value will get replaced
    messageId.Value <- "1"
    element.Attributes.Append(messageId) |> ignore

    let request = rpc.AppendChild(element);
    let item = request.AppendChild(rpc.CreateElement("get-schema"));
    let schema = rpc.CreateAttribute("xmlns")
    schema.Value <- urn
    item.Attributes.Append(schema) |> ignore
    let identifier = item.AppendChild(rpc.CreateElement("identifier"))
    identifier.InnerText <- name
    rpc

let rpc = GetSchemaCommand ("urn:ietf:params:xml:ns:yang:ietf-netconf-monitoring", "tailf-aaa")
rpc.InnerXml

if client.IsConnected = false then Async.RunSynchronously (ConnectAsync client)
let response = client.SendReceiveRpc(rpc)
response.InnerText
