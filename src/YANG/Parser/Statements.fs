﻿// Statements.fs
namespace Yang.Parser

/// Parsers for common statements, that appear in the yang-stmt rule
[<AutoOpen>]
module Statements =
    open System
    open System.Collections.Generic
    open FParsec
    open NLog
    open Yang.Model

    // [RFC 7950, p. 202-204]
    //
    // Many definitions end with the rule stmtend or stmtsep:
    //  stmtend             = optsep (";" / "{" stmtsep "}") stmtsep
    //  optsep              = *(WSP / line-break)
    //  stmtsep             = *(WSP / line-break / unknown-statement)
    //  unknown-statement   = prefix ":" identifier [sep string] optsep
    //                        (";" /
    //                         "{" optsep
    //                             *((yang-stmt / unknown-statement) optsep)
    //                         "}") stmtsep
    //
    // Observe that the rules above encourage to consume without processing all unknown-statement
    // definitions. However, we want to at least keep track of them, and give the user the option to
    // act upon them. Hence, we will use the following modified rules:
    //  stmtend             = optsep (";" / "{" stmtsep "}") optsep
    //  unknown-statement   = prefix ":" identifier [sep string] optsep
    //                        (";" /
    //                         "{" optsep
    //                             *((yang-stmt / unknown-statement) optsep)
    //                         "}") optsep
    // and also process unknown-statement rules.

    // TODO: Namespaces are hierarchical with colon as separator, e.g. urn:example:system

    // Assumption: every parser starts at a point that is not white-space and
    //             consumes all whitespace (including empty statements) that follow it.

    /// Logger for this module
    let private _logger = LogManager.GetCurrentClassLogger()

    let private throw fmt =
        let do_throw (message : string) =
            _logger.Error message
            raise (YangParserException message)
        Printf.ksprintf do_throw fmt

    let private warn fmt = Printf.ksprintf _logger.Warn fmt
    let private debug fmt = Printf.ksprintf _logger.Debug fmt
    let private error fmt = Printf.ksprintf _logger.Error fmt

#if INTERACTIVE
    // The following are used only in interactive (fsi) to help with enabling disabling
    // logging for particular modules.

    type internal Marker = interface end
    let _full_name = typeof<Marker>.DeclaringType.FullName
    let _name = typeof<Marker>.DeclaringType.Name
#endif

    //
    //
    // Helper definitions that consume trailing whitespace
    //
    //

    let inline private read_keyword<'a> : Parser<string, 'a> = Strings.parse_string .>> spaces

    /// Consume whitespace and empty statements.
    let wse<'u> : Parser<unit, 'u> =
        spaces .>> (many (skipChar ';' >>. spaces))

    /// Parses the end of regular statements that finish with a semicolon ';'.
    /// It also consumes empty statements that may follow (multiple semicolons).
    let inline end_of_statement<'a> : Parser<unit, 'a> = skipChar ';' >>. wse

    /// Parses the beginning of a block; it starts with '{'.
    /// It also consumes empty statements at the beginning of the block (multiple semicolons)
    let inline begin_block<'a> : Parser<unit, 'a> = skipChar '{' >>. wse

    /// Parses the end of the block; it should end with '}'.
    /// It also consumes whitespace and empty statements after the end of the block.
    let inline end_block<'a> : Parser<unit, 'a> = skipChar '}' .>> wse

    let inline block<'a> (parser : Parser<Statement, 'a>) : Parser<Statement list, 'a> =
        manyTill parser end_block

    let inline block_generic<'a, 'b> (parser : Parser<'b, 'a>) : Parser<'b list, 'a> =
        manyTill parser end_block

    /// Parser for statements that do not specify a child block; if a block exists, it will be parsed
    /// as a list of generic statements with a custom parser (typically a parser that accepts any valid statement).
    let inline end_of_statement_or_block<'a> (parser : Parser<Statement, 'a>) : Parser<ExtraStatements, 'a> =
        (end_of_statement               |>> (fun _ -> None))
        <|>
        (begin_block >>. (block parser) |>> (fun statements -> Some statements))

    /// Parser for statements that do not specify a child block; if a block exists, it will be parsed
    /// as a list of generic statements with a custom parser (typically a parser that accepts any valid statement).
    let inline end_of_statement_or_block_generic<'a, 'b> (parser : Parser<'b, 'a>) : Parser<'b list option, 'a> =
        (end_of_statement               |>> (fun _ -> None))
        <|>
        (begin_block >>. (block_generic parser) |>> (fun statements -> Some statements))

    /// Parser for statements that define the structure of their child block, which is optional
    /// (i.e. it may not exist).
    let inline make_statement_parser_optional keyword argument body =
        attempt (skipString keyword >>. spaces >>. argument .>> spaces) .>>.
        (
                (end_of_statement                   |>> (fun _ -> None))
            <|> (begin_block >>. (block body)       |>> Some)
        )

    /// Parser for statements that require a child block.
    let inline make_statement_parser_generic<'a, 'T, 'b>
        (keyword    : string)
        (argument   : Parser<'T, 'a>)
        (body       : Parser<'b, 'a>) : Parser<('T * ('b list)), 'a>
        =
            attempt (skipString keyword >>. spaces >>. argument .>> spaces) .>>
            begin_block .>>.
            (block_generic body)

    /// Parser for statements that define the structure of their child block, which is optional
    /// (i.e. it may not exist).
    let inline make_statement_parser_optional_generic<'a, 'T, 'b>
        (keyword    : string)
        (argument   : Parser<'T, 'a>)
        (body       : Parser<'b, 'a>) : Parser<('T * ('b list option)), 'a>
        =
            attempt (skipString keyword >>. spaces >>. argument .>> spaces) .>>.
            (
                    (end_of_statement                       |>> (fun _ -> None))
                <|> (begin_block >>. (block_generic body)   |>> Some)
            )

    /// Parser for statements that require a child block, and do not have an argument.
    let inline make_statement_parser_no_argument_generic<'a, 'b>
        (keyword    : string)
        (body       : Parser<'b, 'a>) : Parser<('b list), 'a>
        =
            attempt (skipString keyword >>. spaces) >>.
            begin_block >>.
            (block_generic body)

    /// Parses an unknown statement; those have an identifier which includes a prefix
    let inline private unknown_statement<'a> (parser : Parser<Statement, 'a>) : Parser<Statement, 'a> =
            Identifier.parse_identifier_with_prefix
            .>> spaces
            .>>. ((end_of_statement_or_block parser
                   |>> (fun body            -> None, body))
                  <|>
                  (read_keyword .>>. end_of_statement_or_block parser
                   |>> (fun (argument, body)    -> Some argument, body))
                 )
            |>> (fun (identifier, (argument, body)) -> Statement.Unknown (UnknownStatement (identifier, argument, body)))

    let inline private yang_keyword_string_statement<'a> (keyword : string, maker) (parser : Parser<Statement, 'a>) : Parser<Statement, 'a> =
        skipString keyword      >>. spaces  >>.
        Strings.parse_string   .>>  spaces .>>.
        end_of_statement_or_block parser
        |>> maker

    let inline private yang_keyword_uri_statement<'a> (keyword : string, maker) (parser : Parser<Statement, 'a>) : Parser<Statement, 'a> =
        skipString keyword              >>. spaces  >>.
        (Strings.parse_string |>> Uri) .>>  spaces .>>.
        end_of_statement_or_block parser
        |>> maker

    //
    //
    // End of helper definitions
    //
    //

    //
    // Definition of generic parser
    //

    let mutable generic_parser_generator : Type option = None
    let generic_parser_implementations = Dictionary<System.Type, obj>()

    /// Parses a yang-stmt [RFC 7950, p. 202].
    /// It should be used for parsing rules with no constraints, e.g.
    // inside unknown-statement rules.
    let parse_statement<'a> =
        // This statement has many call sites. Each of them will initialize the following variables.
        // See for example, the number of calls with the following command in interactive:
        // printfn "Inside parse_statement creation for type %s" typeof<'a>.FullName

        let mutable initialized = false
        let (parse_statement : Parser<Statement, 'a>), (parse_statement_ref : Parser<Statement, 'a> ref) =
            createParserForwardedToRef<Statement, 'a>()

        fun stream ->
            if initialized = false then
                let key = typeof<'a>
                if generic_parser_implementations.ContainsKey(key) then
                    parse_statement_ref := generic_parser_implementations.[key] :?> Parser<Statement, 'a>
                    initialized <- true
                elif generic_parser_generator.IsSome then
                    debug "Constructing generator for %s" key.FullName

                    let ty = generic_parser_generator.Value
                    let generic = ty.GetGenericTypeDefinition()
                    let proper = generic.MakeGenericType(key)
                    let method = proper.GetMethod("Parser")
                    let parser = method.Invoke(null, [| |])
                    generic_parser_implementations.Add(key, parser)
                    parse_statement_ref := parser :?> Parser<Statement, 'a>
                    initialized <- true
                else
                    error "Cannot find generic parser for type %s and do not know how to construct it" key.FullName
                    // The call will fail, the user will get the error that the parser is not implemented

            let result = !parse_statement_ref stream
            result

    //
    // End of definition of generic parser
    //

    /// Parses the rest of statements.
    /// It should be used inside blocks with no constraints, e.g. in unknown statement blocks.
    let parse_many_statements<'a> : Parser<Statement list, 'a> =
        many parse_statement

    let parse_base_statement<'a> : Parser<BaseStatement, 'a> =
        // [RFC 7950, p. 187]
        //base-stmt           = base-keyword sep identifier-ref-arg-str
        //                        stmtend
        make_statement_parser_optional "base" parse_identifier_reference parse_statement
        |>> BaseStatement

    /// Parses a config statement
    let parse_config_statement<'a> : Parser<ConfigStatement, 'a> =
        // [RFC 7950, p. 191]
        //config-stmt         = config-keyword sep
        //                        config-arg-str stmtend
        //config-arg-str      = < a string that matches the rule >
        //                        < config-arg >
        //config-arg          = true-keyword / false-keyword
        make_statement_parser_optional "config" parse_boolean parse_statement
        |>> ConfigStatement

    /// Parses a contact statement
    let parse_contact_statement<'a> : Parser<ContactStatement, 'a> =
        // [RFC 7950, p. 186]
        // contact-stmt        = contact-keyword sep string stmtend
        make_statement_parser_optional "contact" Strings.parse_string parse_statement
        |>> ContactStatement

    /// Parses the default statement
    let parse_default_statement<'a> : Parser<DefaultStatement, 'a> =
        // [RFC 7950, p. 186]
        // default-stmt        = default-keyword sep string stmtend
        make_statement_parser_optional "default" Strings.parse_string parse_statement
        |>> DefaultStatement

    /// Parses a description statement
    let parse_description_statement<'a> : Parser<DescriptionStatement, 'a> =
        // [RFC 7950, p. 186]
        // description-stmt    = description-keyword sep string stmtend
        make_statement_parser_optional "description" Strings.parse_string parse_statement
        |>> DescriptionStatement

    /// Parses an error message statement
    let parse_error_app_tag_statement<'a> : Parser<ErrorAppTagStatement, 'a> =
        // [RFC 7950, p. 192]
        // error-app-tag-stmt  = error-app-tag-keyword sep string stmtend
        make_statement_parser_optional "error-app-tag" Strings.parse_string parse_statement
        |>> ErrorAppTagStatement

    /// Parses an error message statement
    let parse_error_message_statement<'a> : Parser<ErrorMessageStatement, 'a> =
        // [RFC 7950, p. 192]
        // error-message-stmt  = error-message-keyword sep string stmtend
        make_statement_parser_optional "error-message" Strings.parse_string parse_statement
        |>> ErrorMessageStatement

    let parse_fraction_digits_statement<'a> : Parser<FractionDigitsStatement, 'a> =
        // [RFC 7950, 189]
        //fraction-digits-stmt = fraction-digits-keyword sep
        //                        fraction-digits-arg-str stmtend
        make_statement_parser_optional "fraction-digits" Arguments.parse_fraction_digits parse_statement
        |>> FractionDigitsStatement

    /// Parses a key statement
    let parse_key_statement<'a> : Parser<KeyStatement, 'a> =
        // [RFC 7950, p. 195]
        //key-stmt            = key-keyword sep key-arg-str stmtend
        //key-arg-str         = < a string that matches the rule >
        //                        < key-arg >
        //key-arg             = node-identifier *(sep node-identifier)
        make_statement_parser_optional "key" Strings.parse_string parse_statement
        |>> (fun (key, block) -> KeyStatement (Arguments.KeyFromString key, block))

    let parse_mandatory_statement<'a> : Parser<MandatoryStatement, 'a> =
        // [RFC 7950, p. 192]
        //mandatory-stmt      = mandatory-keyword sep
        //                        mandatory-arg-str stmtend
        //mandatory-arg-str   = < a string that matches the rule >
        //                        < mandatory-arg >
        //mandatory-arg       = true-keyword / false-keyword
        make_statement_parser_optional "mandatory" Arguments.parse_boolean parse_statement
        |>> MandatoryStatement

    let parse_max_elements_statement<'a> : Parser<MaxElementsStatement, 'a> =
        // [RFC 7950, p. 192]
        //max-elements-stmt   = max-elements-keyword sep
        //                        max-value-arg-str stmtend
        make_statement_parser_optional "max-elements" parse_max_value parse_statement
        |>> MaxElementsStatement

    let parse_min_elements_statement<'a> : Parser<MinElementsStatement, 'a> =
        // [RFC 7950, p. 192]
        //min-elements-stmt   = min-elements-keyword sep
        //                        min-value-arg-str stmtend
        make_statement_parser_optional "min-elements" parse_min_value parse_statement
        |>> MinElementsStatement

    let parse_modifier_statement<'a> : Parser<ModifierStatement, 'a> =
        // [RFC 7950, p. 190]
        //modifier-stmt       = modifier-keyword sep modifier-arg-str stmtend
        //modifier-arg-str    = < a string that matches the rule >
        //                        < modifier-arg >
        //modifier-arg        = invert-match-keyword
        make_statement_parser_optional "modifier" Arguments.parse_modifier parse_statement
        |>> ModifierStatement

    /// Parses a reference statement
    let parse_namespace_statement<'a> : Parser<NamespaceStatement, 'a> =
        // [RFC 7950, p. 186]
        //namespace-stmt      = namespace-keyword sep uri-str stmtend
        //uri-str             = < a string that matches the rule >
        //                      < URI in RFC 3986 >
        make_statement_parser_optional "namespace" (Strings.parse_string |>> Uri) parse_statement
        |>> NamespaceStatement

    let parse_ordered_by_statement<'a> : Parser<OrderedByStatement, 'a> =
        // [RFC 7950, p. 192]
        //ordered-by-stmt     = ordered-by-keyword sep
        //                        ordered-by-arg-str stmtend
        make_statement_parser_optional "ordered-by" parse_ordered_by parse_statement
        |>> OrderedByStatement

    /// Parses an organization statement
    let parse_organization_statement<'a> : Parser<OrganizationStatement, 'a> =
        // [RFC 7950, p. 186]
        // organization-stmt   = organization-keyword sep string stmtend
        make_statement_parser_optional "organization" Strings.parse_string parse_statement
        |>> OrganizationStatement

    /// Parses a path statement
    let parse_path_statement<'a> : Parser<PathStatement, 'a> =
        // [RFC 7950, p. 190]
        // path-stmt           = path-keyword sep path-arg-str stmtend
        make_statement_parser_optional "path" parse_path parse_statement
        |>> PathStatement

    let parse_position_statement<'a> : Parser<PositionStatement, 'a> =
        // [RFC 7950, p. 191]
        //position-stmt       = position-keyword sep
        //                        position-value-arg-str stmtend
        //position-value-arg-str = < a string that matches the rule >
        //                        < position-value-arg >
        //position-value-arg  = non-negative-integer-value
        make_statement_parser_optional "position" parse_uint32 parse_statement
        |>> PositionStatement

    /// Parses a prefix statement
    let parse_prefix_statement<'a> : Parser<PrefixStatement, 'a> =
        // [RFC 7950, p. 208]
        //prefix-stmt         = prefix-keyword sep prefix-arg-str stmtend
        //prefix-arg-str      = < a string that matches the rule >
        //                      < prefix-arg >
        //prefix-arg          = prefix
        //prefix              = identifier
        make_statement_parser_optional "prefix" Strings.parse_string parse_statement
        |>> PrefixStatement

    /// Parses a presence statement
    let parse_presence_statement<'a> : Parser<PresenceStatement, 'a> =
        // [RFC 7950, p. 192]
        // presence-stmt       = presence-keyword sep string stmtend
        make_statement_parser_optional "presence" Strings.parse_string parse_statement
        |>> PresenceStatement

    /// Parses a reference statement
    let parse_reference_statement<'a> : Parser<ReferenceStatement, 'a> =
        // [RFC 7950, p. 186]
        // reference-stmt      = reference-keyword sep string stmtend
        make_statement_parser_optional "reference" Strings.parse_string parse_statement
        |>> ReferenceStatement

    let parse_require_instance_statement<'a> : Parser<RequireInstanceStatement, 'a> =
        // [RFC 7950, p. 190]
        //require-instance-stmt = require-instance-keyword sep
        //                        require-instance-arg-str stmtend
        //require-instance-arg-str = < a string that matches the rule >
        //                            < require-instance-arg >
        //require-instance-arg = true-keyword / false-keyword
        make_statement_parser_optional "require-instance" parse_boolean parse_statement
        |>> RequireInstanceStatement

    let parse_revision_date_statement<'a> : Parser<RevisionDateStatement, 'a> =
        // [RFC 7950, p. 186]
        // revision-date-stmt  = revision-date-keyword sep revision-date stmtend
        // [RFC 7950, p.207]
        // revision-date-keyword    = %s"revision-date"
        make_statement_parser_optional "revision-date" parse_date parse_statement
        |>> RevisionDateStatement

    let parse_status_statement<'a> : Parser<StatusStatement, 'a> =
        // [RFC 7950, p. 191]
        //status-stmt         = status-keyword sep status-arg-str stmtend
        //status-arg-str      = < a string that matches the rule >
        //                        < status-arg >
        //status-arg          = current-keyword /
        //                        obsolete-keyword /
        //                        deprecated-keyword
        make_statement_parser_optional "status" parse_status parse_statement
        |>> StatusStatement

    let parse_units_statement<'a> : Parser<UnitsStatement, 'a> =
        // [RFC 7950, p. 186]
        // units-stmt          = units-keyword sep string stmtend
        make_statement_parser_optional "units" Strings.parse_string parse_statement
        |>> UnitsStatement

    /// Parses a YANG version information
    let parse_yang_version_statement<'a> : Parser<YangVersionStatement, 'a> =
        // [RFC 7950, p. 185]
        //yang-version-stmt   = yang-version-keyword sep yang-version-arg-str
        //                      stmtend
        //yang-version-arg-str = < a string that matches the rule >
        //                          < yang-version-arg >
        //yang-version-arg    = "1.1"        skipString "reference" >>. spaces >>.

        let do_parse_version (input : string) : Version =
            // Correcting version 1, which is typicall given as 1, instead of 1.0
            let input' =
                if input.Contains(".") = false then sprintf "%s.0" input
                else input
            Version.Parse input'

        make_statement_parser_optional "yang-version" (Strings.parse_string |>> do_parse_version) parse_statement
        |>> YangVersionStatement

    let parse_yin_element_statement<'a> : Parser<YinElementStatement, 'a> =
        // [RFC 7950, p. 187]
        //yin-element-stmt    = yin-element-keyword sep yin-element-arg-str
        //                        stmtend
        //yin-element-arg-str = < a string that matches the rule >
        //                        < yin-element-arg >
        //yin-element-arg     = true-keyword / false-keyword
        make_statement_parser_optional "yin-element" Arguments.parse_boolean parse_statement
        |>> YinElementStatement

    let parse_unique_statement<'a> : Parser<UniqueStatement, 'a> =
        // [RFC 7950, p. 195]
        // unique-stmt         = unique-keyword sep unique-arg-str stmtend
        make_statement_parser_optional "unique" parse_unique parse_statement
        |>> UniqueStatement

    /// Helper method to parse an unknown statement
    let parse_unknown_statement<'a> : Parser<UnknownStatement, 'a> =
        // Unknown statement do not necessarily have an argument.
        Identifier.parse_identifier_with_prefix
        .>> spaces
        .>>. ((end_of_statement_or_block parse_statement
                |>> (fun body            -> None, body))
                <|>
                (Strings.parse_string .>>
                 spaces .>>. end_of_statement_or_block parse_statement
                |>> (fun (argument, body)    -> Some argument, body))
             )
        |>> (fun (identifier, (argument, body)) -> UnknownStatement (identifier, argument, body))

    let parse_value_statement<'a> : Parser<ValueStatement, 'a> =
        // [RFC 7950, p. 193]
        //value-stmt          = value-keyword sep integer-value-str stmtend
        //integer-value-str   = < a string that matches the rule >
        //                        < integer-value >
        make_statement_parser_optional "value" parse_int64 parse_statement
        |>> ValueStatement

    //
    // The definitions below depend on some definitions above
    //

    let parse_argument_body_statement<'a> : Parser<ArgumentBodyStatement, 'a> =
            (parse_yin_element_statement    |>> ArgumentBodyStatement.YinElement)
        <|> (parse_unknown_statement        |>> ArgumentBodyStatement.Unknown)

    let parse_argument_statement<'a> : Parser<ArgumentStatement, 'a> =
        // [RFC 7950, p. 187]
        //argument-stmt       = argument-keyword sep identifier-arg-str optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            [yin-element-stmt]
        //                        "}") stmtsep
        make_statement_parser_optional_generic "argument" parse_identifier parse_argument_body_statement
        |>> ArgumentStatement

    let parse_belongs_to_body_statement<'a> : Parser<BelongsToBodyStatement, 'a> =
            (parse_prefix_statement     |>> BelongsToBodyStatement.Prefix)
        <|> (parse_unknown_statement    |>> BelongsToBodyStatement.Unknown)

    let parse_belongs_to_statement<'a> : Parser<BelongsToStatement, 'a> =
        // [RFC 7950, p. 186]
        //belongs-to-stmt     = belongs-to-keyword sep identifier-arg-str
        //                        optsep
        //                        "{" stmtsep
        //                            prefix-stmt
        //                        "}" stmtsep
        make_statement_parser_generic "belongs-to" parse_identifier parse_belongs_to_body_statement
        |>> BelongsToStatement

    let parse_extension_body_statement<'a> : Parser<ExtensionBodyStatement, 'a> =
            (parse_argument_statement       |>> ExtensionBodyStatement.Argument)
        <|> (parse_status_statement         |>> ExtensionBodyStatement.Status)
        <|> (parse_description_statement    |>> ExtensionBodyStatement.Description)
        <|> (parse_reference_statement      |>> ExtensionBodyStatement.Reference)
        <|> (parse_unknown_statement        |>> ExtensionBodyStatement.Unknown)

    let parse_extension_statement<'a> : Parser<ExtensionStatement, 'a> =
        // [RFC 7950, p. 187]
        //extension-stmt      = extension-keyword sep identifier-arg-str optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            ;; these stmts can appear in any order
        //                            [argument-stmt]
        //                            [status-stmt]
        //                            [description-stmt]
        //                            [reference-stmt]
        //                        "}") stmtsep
        make_statement_parser_optional_generic "extension" parse_identifier parse_extension_body_statement
        |>> ExtensionStatement

    let parse_length_body_statement<'a> : Parser<LengthBodyStatement, 'a> =
            (parse_error_message_statement  |>> LengthBodyStatement.ErrorMessage)
        <|> (parse_error_app_tag_statement  |>> LengthBodyStatement.ErrorAppTag)
        <|> (parse_description_statement    |>> LengthBodyStatement.Description)
        <|> (parse_reference_statement      |>> LengthBodyStatement.Reference)
        <|> (parse_unknown_statement        |>> LengthBodyStatement.Unknown)

    /// Parses a length statement
    let parse_length_statement<'a> : Parser<LengthStatement, 'a> =
        // [RFC 7950, p. 189]
        //length-stmt         = length-keyword sep length-arg-str optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            ;; these stmts can appear in any order
        //                            [error-message-stmt]
        //                            [error-app-tag-stmt]
        //                            [description-stmt]
        //                            [reference-stmt]
        //                        "}") stmtsep
        // TODO: Check and enforce cardinality constraints for length-stmt
        make_statement_parser_optional_generic "length" parse_length parse_length_body_statement
        |>> LengthStatement

    let parse_must_body_statement<'a> : Parser<MustBodyStatement, 'a> =
            (parse_error_message_statement  |>> MustBodyStatement.ErrorMessage)
        <|> (parse_error_app_tag_statement  |>> MustBodyStatement.ErrorAppTag)
        <|> (parse_description_statement    |>> MustBodyStatement.Description)
        <|> (parse_reference_statement      |>> MustBodyStatement.Reference)
        <|> (parse_unknown_statement        |>> MustBodyStatement.Unknown)

    let parse_must_statement<'a> : Parser<MustStatement, 'a> =
        // [RFC 7950, p. 192]
        //must-stmt           = must-keyword sep string optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            ;; these stmts can appear in any order
        //                            [error-message-stmt]
        //                            [error-app-tag-stmt]
        //                            [description-stmt]
        //                            [reference-stmt]
        //                        "}") stmtsep
        make_statement_parser_optional_generic "must" Strings.parse_string parse_must_body_statement
        |>> MustStatement

    let parse_pattern_body_statement<'a> : Parser<PatternBodyStatement, 'a> =
            (parse_modifier_statement           |>> PatternBodyStatement.Modifier)
        <|> (parse_error_message_statement      |>> PatternBodyStatement.ErrorMessage)
        <|> (parse_error_app_tag_statement      |>> PatternBodyStatement.ErrorAppTag)
        <|> (parse_description_statement        |>> PatternBodyStatement.Description)
        <|> (parse_reference_statement          |>> PatternBodyStatement.Reference)
        <|> (parse_unknown_statement            |>> PatternBodyStatement.Unknown)

    let parse_pattern_statement<'a> : Parser<PatternStatement, 'a> =
        // [RFC 7950, p. 190]
        //pattern-stmt        = pattern-keyword sep string optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            ;; these stmts can appear in any order
        //                            [modifier-stmt]
        //                            [error-message-stmt]
        //                            [error-app-tag-stmt]
        //                            [description-stmt]
        //                            [reference-stmt]
        //                        "}") stmtsep
        make_statement_parser_optional_generic "pattern" Strings.parse_string parse_pattern_body_statement
        |>> PatternStatement

    let parse_range_body_statement<'a> : Parser<RangeBodyStatement, 'a> =
            (parse_error_message_statement   |>> RangeBodyStatement.ErrorMessage)
        <|> (parse_error_app_tag_statement   |>> RangeBodyStatement.ErrorAppTag)
        <|> (parse_description_statement     |>> RangeBodyStatement.Description)
        <|> (parse_reference_statement       |>> RangeBodyStatement.Reference)
        <|> (parse_unknown_statement         |>> RangeBodyStatement.Unknown)

    /// Parses a range stateement
    let parse_range_statement<'a> : Parser<RangeStatement, 'a> =
        // [RFC 7950, p. 189]
        //range-stmt          = range-keyword sep range-arg-str optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            ;; these stmts can appear in any order
        //                            [error-message-stmt]
        //                            [error-app-tag-stmt]
        //                            [description-stmt]
        //                            [reference-stmt]
        //                        "}") stmtsep

        // TODO: Unit tests for range statement
        make_statement_parser_optional_generic "range" parse_range parse_range_body_statement
        |>> RangeStatement

    let parse_when_body_statement<'a> : Parser<WhenBodyStatement, 'a> =
            (parse_description_statement    |>> WhenBodyStatement.Description)
        <|> (parse_reference_statement      |>> WhenBodyStatement.Reference)
        <|> (parse_unknown_statement        |>> WhenBodyStatement.Unknown)

    let parse_when_statement<'a> : Parser<WhenStatement, 'a> =
        // [RFC 7950, p. 199]
        //when-stmt           = when-keyword sep string optsep
        //                        (";" /
        //                        "{" stmtsep
        //                            ;; these stmts can appear in any order
        //                            [description-stmt]
        //                            [reference-stmt]
        //                        "}") stmtsep
        // TODO: Check and enforce cardinality constraints for when-stmt
        // TODO: Often the when-stmt contain xpath conditions on the schema; provide support for parsing them.
        //       E.g. when "../crypto = 'mc:aes'";
        make_statement_parser_optional_generic "when" Strings.parse_string parse_when_body_statement
        |>> WhenStatement
