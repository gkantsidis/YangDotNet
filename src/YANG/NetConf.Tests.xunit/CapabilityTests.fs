namespace Yang.NetConf.Tests

module CapabilityTests =
    open System
    open Xunit
    open Yang.NetConf

     [<Theory>]
     [<InlineData("urn:ietf:params:xml:ns:netconf:base:1.0?module=ietf-netconf&revision=2011-06-01", 1, 0)>]
     [<InlineData("urn:ietf:params:xml:ns:netconf:base:1.1", 1, 1)>]
     let ``Parse protocol version from URN`` (urn:string, major:int, minor:int) =
        let expected = Version(major, minor)
        let version = Capability.TryGetVersion (Uri(urn))
        Assert.True(version.IsSome)
        Assert.Equal(expected, version.Value)
