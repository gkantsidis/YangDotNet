﻿namespace Yang.Parser.Tests

module TypesTests =
    open System
    open Xunit
    open Yang.Model
    open Yang.Parser.Types

    [<Fact>]
    let ``parse simple string type`` () =
        let input = """type string;"""
        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(IdentifierReference.Make "string", id)
        Assert.Equal(None, restriction)

    [<Fact>]
    let ``parse string type with empty body`` ()=
        let input = """type string {}"""
        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(IdentifierReference.Make "string", id)
        Assert.Equal(None, restriction)

    [<Fact>]
    let ``parse string type with simple length constraint`` () =
        let input = """type string {
                 length "1 .. 128";
               }"""
        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(IdentifierReference.Make "string", id)
        Assert.NotEqual(None, restriction)

    [<Fact>]
    let ``parse string type with custom constraints`` () =
        let input = """type string {
                 junos:posix-pattern "^.{1,64}$";
                 junos:pattern-message "Must be string of 64 characters or less";
               }"""
        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(Identifier.IdentifierReference.Make "string", id)
        Assert.True(restriction.IsSome)
        let string_restriction = TypeBodyStatement.AsStringRestrictions restriction.Value
        Assert.True(string_restriction.IsSome)
        Assert.Equal(2, string_restriction.Value.Length)
        // TODO: Check that the string restrictions are unknown

    [<Fact>]
    let ``type string with type restrictions appear before custom extensions`` () =
        let input = """type string {
             length "1 .. 128";
             junos:posix-pattern "^.{1,64}$";
             junos:pattern-message "Must be string of 64 characters or less";
           }"""

        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(Identifier.IdentifierReference.Make "string", id)
        Assert.True(restriction.IsSome)
        let string_restriction = TypeBodyStatement.AsStringRestrictions restriction.Value
        Assert.True(string_restriction.IsSome)
        Assert.Equal(3, string_restriction.Value.Length)
        // TODO: Check that the string restrictions are correct

    [<Fact>]
    let ``parse string type with custom extensions and restrictions`` () =
        let input = """type string {
                 junos:posix-pattern "^.{1,64}$";
                 junos:pattern-message "Must be string of 64 characters or less";
                 length "1 .. 128";
               }"""
        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(Identifier.IdentifierReference.Make "string", id)
        Assert.True(restriction.IsSome)
        let string_restriction = TypeBodyStatement.AsStringRestrictions restriction.Value
        Assert.True(string_restriction.IsSome)
        Assert.Equal(3, string_restriction.Value.Length)
        // TODO: Check that the string restrictions are correct

    [<Fact>]
    let ``fail when string type length restriction appears twice`` () =
        let input = """type string {
             junos:posix-pattern "^.{1,64}$";
             junos:pattern-message "Must be string of 64 characters or less";
             length "1 .. 64";
             length "1 .. 128";
           }"""

        Assert.ThrowsAny<Exception>(
            fun _ -> FParsecHelper.apply parse_type_statement input |> ignore
        )

    [<Fact>]
    let ``parse string type with both length and pattern restriction`` () =
        let input = """type string {
             junos:posix-pattern "^.{1,64}$";
             junos:pattern-message "Must be string of 64 characters or less";
             length "1 .. 64";
             pattern "^.{1,64}$";
           }"""

        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(Identifier.IdentifierReference.Make "string", id)
        Assert.True(restriction.IsSome)
        let string_restriction = TypeBodyStatement.AsStringRestrictions restriction.Value
        Assert.True(string_restriction.IsSome)
        Assert.Equal(4, string_restriction.Value.Length)
        // TODO: Check that the string restrictions are correct

    [<Fact>]
    let ``parse string with three pattern restrictions, length, and unknowns`` () =
        let input = """type string {
             junos:posix-pattern "^.{1,64}$";
             junos:pattern-message "Must be string of 64 characters or less";
             length "1 .. 64";
             pattern "^.{1,62}$";
             pattern "^.{1,64}$";
             pattern "^.{1,60}$";
           }"""

        let (TypeStatement (id, restriction)) = FParsecHelper.apply parse_type_statement input
        Assert.Equal(Identifier.IdentifierReference.Make "string", id)
        Assert.True(restriction.IsSome)
        let string_restriction = TypeBodyStatement.AsStringRestrictions restriction.Value
        Assert.True(string_restriction.IsSome)
        Assert.Equal(6, string_restriction.Value.Length)
        // TODO: Check that the string restrictions are correct
