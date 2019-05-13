namespace Yang.NetConf

module Capability =
    open System
    open Yang.Model

    let ReadCapabilities (document : Xml.XmlDocument) =
        let raw = ((document.Item "hello").Item "capabilities").ChildNodes
        List.init raw.Count (fun i -> raw.Item i)
        |> List.map (
            fun capability ->
                assert (capability.LocalName.Equals("capability", StringComparison.InvariantCultureIgnoreCase))
                assert (capability.ChildNodes.Count = 1)
                assert (capability.FirstChild.LocalName.Equals("#text", StringComparison.InvariantCultureIgnoreCase))
                capability.FirstChild.Value
        )

    let TryGetVersion (uri : Uri) =
        if uri.Scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase) then
            if uri.Segments.Length > 0 then
                match Version.TryParse(uri.Segments.[uri.Segments.Length - 1]) with
                | true, version     -> Some version
                | false, _          -> None
            else None
        elif uri.Scheme.Equals("urn", StringComparison.InvariantCultureIgnoreCase) then
            let index = uri.AbsolutePath.LastIndexOf(':')
            if index < 1 then None
            else
                let suffix = uri.AbsolutePath.Substring(index + 1)
                match Version.TryParse(suffix) with
                | true, version     -> Some version
                | false, _          -> None
        else
            None

    let GetOptions (uri : Uri) =
        if uri.Query.Length = 0 then Map.empty
        else
            assert (uri.Query.StartsWith("?"))
            uri.Query.Substring(1).Split('&')
            |> Seq.map(
                fun option ->
                    let index = option.IndexOf('=')
                    assert(index > 0)
                    let name = option.Substring(0, index)
                    let value = option.Substring(index+1)
                    name, value
            )
            |> Map.ofSeq


    let netconf_capability_prefix = "urn:ietf:params:netconf:capability:"

    type Capability = {
        Name        : Uri
        Prefix      : Uri
        Options     : Map<string, string>
        Revision    : Arguments.Date option
        Version     : Version option
    }
    with
        static member Make (name : string) =
            assert (System.String.IsNullOrWhiteSpace(name) = false)
            let uri = Uri(name)
            let baseUri =
                uri.AbsoluteUri.Substring(0, uri.AbsoluteUri.Length - uri.Query.Length) |> Uri
            let options = GetOptions uri

            let revision =
                    match options.TryFind "revision" with
                    | None      -> None
                    | Some rev  -> Arguments.Date.Make rev |> Some

            {
                Name        = uri
                Prefix      = baseUri
                Options     = options
                Revision    = revision
                Version     = TryGetVersion uri
            }

        member this.NetConfCapabilityName =
            if this.Prefix.AbsoluteUri.StartsWith(netconf_capability_prefix, StringComparison.InvariantCultureIgnoreCase) then
                let ending = this.Prefix.AbsoluteUri.Substring(netconf_capability_prefix.Length)
                let index = ending.IndexOf(':')
                assert(index > 0)
                let name = ending.Substring(0, index)
                Some name
            else None


    type StandardCapabilitity =
    | WritableRunning
    | CandidateConfiguration
    | ConfirmedCommit
    | RollbackOnError
    | Validate
    | DistinctStartup
    | Url of string list
    | XPath
    with
        static member TryMake (capability : Capability) =
            match capability.NetConfCapabilityName with
            | None      -> None
            | Some name ->
                if name.Equals("writable-running", StringComparison.InvariantCultureIgnoreCase)     then Some WritableRunning
                elif name.Equals("candidate", StringComparison.InvariantCultureIgnoreCase)          then Some CandidateConfiguration
                elif name.Equals("confirmed-commit", StringComparison.InvariantCultureIgnoreCase)   then Some ConfirmedCommit
                elif name.Equals("rollback-on-error", StringComparison.InvariantCultureIgnoreCase)  then Some RollbackOnError
                elif name.Equals("validate", StringComparison.InvariantCultureIgnoreCase)           then Some Validate
                elif name.Equals("startup", StringComparison.InvariantCultureIgnoreCase)            then Some DistinctStartup
                elif name.Equals("url", StringComparison.InvariantCultureIgnoreCase)                then
                    assert(capability.Options.ContainsKey("scheme"))
                    let options = capability.Options.["scheme"]
                    Some (Url (options.Split(',') |> Array.toList))
                elif name.Equals("xpath", StringComparison.InvariantCultureIgnoreCase)              then Some XPath
                else None
