﻿namespace Yang.Parser.Tests

open Xunit

[<Collection("Yang Parser")>]
module StatementsTests =
    open FParsec
    open Xunit
    open Yang.Model
    open Yang.Parser
    open Yang.Parser.Statements
    open Arguments.Length

    [<Theory>]
    [<InlineData("contact 'Joe Dev'; contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev';contact 'Jim Hacker';;")>]
    [<InlineData("contact 'Joe Dev';;contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev'; ; contact 'Jim Hacker';;")>]
    [<InlineData("contact 'Joe Dev';; contact 'Jim Hacker';;;")>]
    [<InlineData("contact 'Joe Dev'; ;contact 'Jim Hacker';;;")>]
    [<InlineData("contact 'Joe Dev';;;;;;contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev'; ; ; ; ; ; contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { } contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { }; contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { ; } contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { ; } ; contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { opt:when 'true'; } contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { ; opt:when 'true'; } contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { opt:when 'true'; ;} contact 'Jim Hacker';")>]
    [<InlineData("contact 'Joe Dev' { ;opt:when 'true'; } contact 'Jim Hacker';")>]
    let ``parse statements with empty statements in between`` (input) =
        let result = FParsecHelper.apply (many parse_contact_statement) input
        Assert.Equal(2, result.Length)

    [<Fact>]
    let ``parse base statement simple`` () =
        let input = """base if:interface-type;"""
        let (BaseStatement (id, body)) = FParsecHelper.apply parse_base_statement input
        Assert.Equal("if:interface-type", id.Value)
        Assert.True(body.IsNone)

    [<Fact>]
    let ``parse base statement with quotes in name`` () =
        let input = """base "lsp-role";"""
        let (BaseStatement (id, body)) = FParsecHelper.apply parse_base_statement input
        Assert.Equal("lsp-role", id.Value)
        Assert.True(body.IsNone)

    [<Fact>]
    let ``parse belongs-to statement simple`` () =
        let input = """belongs-to "openconfig-mpls" {
    prefix "mpls";
  }"""
        let (BelongsToStatement (id, body)) = FParsecHelper.apply parse_belongs_to_statement input
        Assert.True(id.IsValid)
        Assert.Equal("openconfig-mpls", id.Value)
        Assert.Equal(1, body.Length)
        let prefix = body.Head
        Assert.True(BelongsToBodyStatement.IsPrefix prefix)
        let p = BelongsToBodyStatement.AsPrefix prefix
        Assert.True(p.IsSome)
        let (PrefixStatement (pp, extra)) = p.Value
        Assert.Equal("mpls", pp)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse description statement 1`` () =
        let input = """description
    "Share-Shaping.";"""
        let (DescriptionStatement (description, extra)) = FParsecHelper.apply parse_description_statement input
        Assert.Equal("Share-Shaping.", description)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse description statement simple`` () =
        let input = "description 'help';"
        let result = FParsecHelper.apply parse_statement input
        match result with
        | Statement.Description (DescriptionStatement (description, extra)) ->
            Assert.Equal("help", description)
            Assert.True(extra.IsNone)
        | _ ->
            failwithf "Expected Statement.Description, got %A" result

    [<Fact>]
    let ``parse many description statement simple`` () =
        let input = "description 'help';"
        let result = FParsecHelper.apply (many parse_statement) input
        Assert.Equal(1, result.Length)
        let result = result.Head
        match result with
        | Statement.Description (DescriptionStatement (description, extra)) ->
            Assert.Equal("help", description)
            Assert.True(extra.IsNone)
        | _ ->
            failwithf "Expected Statement.Description, got %A" result

    [<Fact>]
    let ``parse extension statement with unknown extension`` () =
        let input = """extension junos-val-as-xml-tag {
    tailf:use-in "leaf";
    description
      "Internal extension to handle non-YANG JUNOS data models.
      Use only for key enumeration leafs.";
  }"""
        let (ExtensionStatement (id, body)) = FParsecHelper.apply parse_extension_statement input
        Assert.True(id.IsValid)
        Assert.Equal("junos-val-as-xml-tag", id.Value)
        Assert.True(body.IsSome)
        Assert.Equal(2, body.Value.Length)
        let unknown = body.Value.Head
        Assert.True(ExtensionBodyStatement.IsUnknown unknown)
        let s1 = ExtensionBodyStatement.AsUnknown unknown
        Assert.True(s1.IsSome)
        let (UnknownStatement (id, label, body2)) = s1.Value
        Assert.Equal("tailf:use-in", id.Value)
        Assert.True(label.IsSome)
        Assert.Equal("leaf", label.Value)
        Assert.True(body2.IsNone)
        Assert.True(ExtensionBodyStatement.IsDescription (body.Value.Item 1))

    [<Fact>]
    let ``parse identity statement`` () =
        let input = """identity parse_identity_statement {
    base if:interface-type;
    description
      "This identity is used as a base for all xPON interface types
       defined by the BBF that are not in the 'ifType definitions'
       registry maintained by IANA.";
  }"""
        let (IdentityStatement (id, body)) = FParsecHelper.apply parse_identity_statement input
        Assert.Equal("parse_identity_statement", id.Value)
        Assert.True(body.IsSome)
        Assert.Equal(2, body.Value.Length)

    [<Fact>]
    let ``parse key statement that spans two lines`` () =
        let input = """key "source-port destination-port
               source-address destination-address";"""
        let (KeyStatement (Arguments.Key key, extra)) = FParsecHelper.apply parse_key_statement input
        Assert.Equal(4, key.Length)
        Assert.Equal("source-port",         key.Item(0).Value)
        Assert.Equal("destination-port",    key.Item(1).Value)
        Assert.Equal("source-address",      key.Item(2).Value)
        Assert.Equal("destination-address", key.Item(3).Value)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse length statement value simple`` () =
        let input = """length "8";"""
        let (LengthStatement (Length result, body)) = FParsecHelper.apply parse_length_statement input
        Assert.Equal(1, result.Length)
        Assert.True(body.IsNone)
        let length = result.Head
        Assert.True(length._IsSingle)
        let value = length.AsSingle
        Assert.True(value.IsSome)
        Assert.True(value.Value._IsNumber)
        let number = value.Value.AsNumber
        Assert.True(number.IsSome)
        Assert.Equal(8uL, number.Value)

    [<Fact>]
    let ``parse length statement range simple`` () =
        let input = """length "1 .. 128";"""
        let (LengthStatement (Length result, body)) = FParsecHelper.apply parse_length_statement input
        Assert.Equal(1, result.Length)
        Assert.True(body.IsNone)
        let length = result.Head
        Assert.True(length._IsRange)
        let range = length.AsRange
        Assert.True(range.IsSome)
        let (left, right) = range.Value
        Assert.True(left._IsNumber)
        Assert.True(right._IsNumber)
        let left' = left.AsNumber
        let right' = right.AsNumber
        Assert.True(left'.IsSome)
        Assert.True(right'.IsSome)
        Assert.Equal(1uL, left'.Value)
        Assert.Equal(128uL, right'.Value)

    [<Fact>]
    let ``parse must statement`` () =
        let input = """must "count(*) > 1" {
        tailf:dependency ".";
      }"""
        let (MustStatement (condition, body)) = FParsecHelper.apply parse_must_statement input
        Assert.Equal("count(*) > 1", condition)
        Assert.True(body.IsSome)
        Assert.Equal(1, body.Value.Length)
        let inner = body.Value.Head
        Assert.True(MustBodyStatement.IsUnknown inner)
        let unknown = MustBodyStatement.AsUnknown inner
        Assert.True(unknown.IsSome)
        let (UnknownStatement (id, label, extra)) = unknown.Value
        Assert.Equal("tailf:dependency", id.Value)
        Assert.Equal(Some ".", label)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse must statement with conditions`` () =
        let input = """must "/if:interfaces/if:interface[if:name = current()]"
                + "/if:type = 'ianaift:ethernetCsmacd'" {
                 description
                     "The type of a slave interface must be
                      'ethernetCsmacd'.";
             }"""
        let (MustStatement (must, body)) = FParsecHelper.apply parse_must_statement input
        Assert.Equal("/if:interfaces/if:interface[if:name = current()]/if:type = 'ianaift:ethernetCsmacd'", must)
        Assert.True(body.IsSome)
        Assert.Equal(1, body.Value.Length)
        // TODO: Eventually, we also want to parse and test the conditionals inside the when string

    [<Fact>]
    let ``parse path statement in multiline string`` () =
        let input = """path "/nw:networks/nw:network/nw:node/tet:te/"
             + "tet:te-node-attributes/tet:connectivity-
matrices/tet:connectivity-matrix/tet:id";"""
        let (PathStatement (path, extra)) = FParsecHelper.apply parse_path_statement input
        Assert.True(extra.IsNone)
        Assert.True(path._IsAbsolute)
        let path' = path.AsAbsolute
        Assert.True(path'.IsSome)
        let items = path'.Value.Path
        Assert.Equal(8, items.Length)
        // TODO: Add extras tests for the parts of the path

    [<Theory>]
    [<InlineData("input/state input/symbol")>]
    [<InlineData("input/state  input/symbol")>]
    [<InlineData("input/state\tinput/symbol")>]
    [<InlineData("input/state\ninput/symbol")>]
    let ``parse unique statement`` (input) =
        let input = sprintf "unique \"%s\";" input
        let (UniqueStatement ((Arguments.Unique unique as u), extra)) = FParsecHelper.apply parse_unique_statement input
        Assert.True(extra.IsNone)
        Assert.Equal(2, unique.Length)
        Assert.Equal("input/state", unique.[0].Value)
        Assert.Equal("input/symbol", unique.[1].Value)
        Assert.Equal("input/state input/symbol", u.Value)

    [<Fact>]
    let ``parse unknown statement`` () =
        let input = """junos:posix-pattern "^.{1,64}$";"""
        let (UnknownStatement (id, label, body)) = FParsecHelper.apply parse_unknown_statement input
        Assert.True(id.IsValid)
        Assert.Equal("junos", id._Prefix)
        Assert.Equal("junos:posix-pattern", id.Value)
        Assert.True(label.IsSome)
        Assert.Equal("^.{1,64}$", label.Value)
        Assert.True(body.IsNone)

    [<Fact>]
    let ``parse unknown statement 2`` () =
        let input = """ext:bit "Physical Layer" {
            position 0;
          }"""
        let (UnknownStatement (id, label, body)) = FParsecHelper.apply parse_unknown_statement input
        Assert.True(id.IsValid)
        Assert.Equal("ext", id._Prefix)
        Assert.Equal("ext:bit", id.Value)

        Assert.True(label.IsSome)
        Assert.Equal("Physical Layer", label.Value)
        
        Assert.True(body.IsSome)
        Assert.Equal(1, body.Value.Length)
        // TODO: All test that the body statement is of type position

    [<Fact>]
    let ``parse unknown statement with uses in the body`` () =
        let input = """rc:yang-data voucher-request-artifact {
    uses voucher-request-grouping;
  }"""
        let (UnknownStatement (id, label, body)) = FParsecHelper.apply parse_unknown_statement input
        Assert.True(id.IsValid)
        Assert.Equal("rc:yang-data", id.Value)
        Assert.Equal(Some "voucher-request-artifact", label)
        Assert.True(body.IsSome)
        Assert.Equal(1, body.Value.Length)
        let statement = body.Value.Head
        Assert.True(Statement.IsUses statement)

    [<Fact>]
    let ``parse unknown statement with keyword prefix`` () =
        let input = """config:required-identity sal:dom-data-store;"""
        let statement =
            FParsecHelper.apply (
                    (parse_config_statement  |>> Statement.Config)
                <|> (parse_unknown_statement |>> Statement.Unknown)) input
        Assert.True(Statement.IsUnknown statement)
        let unknown = Statement.AsUnknown statement
        Assert.True(unknown.IsSome)
        let (UnknownStatement (id, label, extra)) = unknown.Value
        Assert.True(id.IsValid)
        Assert.Equal("config:required-identity", id.Value)
        Assert.True(label.IsSome)
        Assert.Equal("sal:dom-data-store", label.Value)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse unknown statement nested`` () =
        let input = """tailf:callpoint "ncs-rfs-service-hook" {
        tailf:transaction-hook "subtree" {
          tailf:invocation-mode "per-transaction";
        }
      }"""
        let (UnknownStatement (id, label, body)) = FParsecHelper.apply parse_unknown_statement input
        Assert.True(id.IsValid)
        Assert.Equal("tailf:callpoint", id.Value)
        Assert.True(label.IsSome)
        Assert.Equal("ncs-rfs-service-hook", label.Value)
        Assert.True(body.IsSome)
        Assert.Equal(1, body.Value.Length)

        let inner1 = body.Value.Head
        Assert.True(Statement.IsUnknown inner1)
        let unknown1 = Statement.AsUnknown inner1
        Assert.True(unknown1.IsSome)
        let (UnknownStatement (id2, label2, body2)) = unknown1.Value
        Assert.Equal("tailf:transaction-hook", id2.Value)
        Assert.Equal(Some "subtree", label2)
        Assert.True(body2.IsSome)
        Assert.Equal(1, body2.Value.Length)

        let inner2 = body2.Value.Head
        Assert.True(Statement.IsUnknown inner2)
        let unknown2 = Statement.AsUnknown inner2
        Assert.True(unknown2.IsSome)
        let (UnknownStatement (id3, label3, body3)) = unknown2.Value
        Assert.Equal("tailf:invocation-mode", id3.Value)
        Assert.Equal(Some "per-transaction", label3)
        Assert.True(body3.IsNone)

    [<Fact>]
    let ``parse when statement`` () =
        let input = """when "../crypto = 'mc:aes'";"""
        let (WhenStatement (statement, body)) = FParsecHelper.apply parse_when_statement input
        Assert.True(body.IsNone)
        Assert.Equal("../crypto = 'mc:aes'", statement)

    [<Fact>]
    let ``parse when statement long`` () =
        let input = """when "not(../../prioritymodefq='is4cos' or ../../prioritymodefq='ispriority') or ../../prioritymodefq='notpriority'";"""
        let (WhenStatement (statement, body)) = FParsecHelper.apply parse_when_statement input
        Assert.True(body.IsNone)
        Assert.Equal("not(../../prioritymodefq='is4cos' or ../../prioritymodefq='ispriority') or ../../prioritymodefq='notpriority'", statement)

    [<Fact>]
    let ``parse yang-version`` () =
        let input = """yang-version 1.1;"""
        let (YangVersionStatement (version, extra)) = FParsecHelper.apply parse_yang_version_statement input
        Assert.Equal(1, version.Major)
        Assert.Equal(1, version.Minor)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse yang-version with double quote`` () =
        let input = """yang-version "1.1";"""
        let (YangVersionStatement (version, extra)) = FParsecHelper.apply parse_yang_version_statement input
        Assert.Equal(1, version.Major)
        Assert.Equal(1, version.Minor)
        Assert.True(extra.IsNone)

    [<Fact>]
    let ``parse yang-version old style`` () =
        let input = """yang-version 1;"""
        let (YangVersionStatement (version, extra)) = FParsecHelper.apply parse_yang_version_statement input
        Assert.Equal(1, version.Major)
        Assert.Equal(0, version.Minor)
        Assert.True(extra.IsNone)
