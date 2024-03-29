﻿namespace Yang.Parser.Tests

module DeviateTests =
    open FParsec
    open Xunit
    open Yang.Parser
    open Yang.Model

    let private apply = FParsecHelper.apply

    let ``parse deviate add statement`` () =
        let input = """deviate add;"""
        let (DeviateAddStatement extra) = apply parse_deviate_add_statement input
        Assert.True(extra.IsNone)

    let ``parse deviate add statement in quotes`` () =
        let input = """deviate "add";"""
        let (DeviateAddStatement extra) = apply parse_deviate_add_statement input
        Assert.True(extra.IsNone)

    let ``parse deviate not-supported statement`` () =
        let input = """deviate not-supported;"""
        let statement =
            apply (    (parse_deviate_add_statement           |>> Statement.DeviateAdd)
                   <|> (parse_deviate_not_supported_statement |>> Statement.DeviateNotSupported)
                  ) input

        Assert.True(Statement.IsDeviateNotSupported statement)
        let not_supported = Statement.AsDeviateNotSupported statement
        Assert.True(not_supported.IsSome)
        let (DeviateNotSupportedStatement extra) = not_supported.Value
        Assert.True(extra.IsNone)

    let ``parse deviate not-supported statement in quotes`` () =
        let input = """deviate "not-supported";"""
        let (DeviateNotSupportedStatement extra) = apply parse_deviate_not_supported_statement input
        Assert.True(extra.IsNone)
