﻿// Identifier.fs

namespace Yang.Model

/// Parsers and types for YANG identifiers
module Identifier =
    open System
    open NLog

    /// Logger for this module
    let private _logger = LogManager.GetCurrentClassLogger()

#if INTERACTIVE
    // The following are used only in interactive (fsi) to help with enabling disabling
    // logging for particular modules.

    type internal Marker = interface end
    let _full_name = typeof<Marker>.DeclaringType.FullName
    let _name = typeof<Marker>.DeclaringType.Name
#endif

    let private isAsciiIdStart c = Char.IsLetter(c) || c = '_'
    let private isAsciiIdContinue c =
        Char.IsLetterOrDigit(c) || c = '_' || c = '-' || c = '.'

    /// Checks whether a string is a valid identifier name
    let is_identifier_valid (input : string) =
        System.String.IsNullOrWhiteSpace(input) = false &&
        isAsciiIdStart (input.Chars 0) &&
        (String.forall isAsciiIdContinue input)

    /// YANG Identifier
    [<StructuredFormatDisplay("{Value}")>]
    type Identifier = private | String of string
    with
        /// <summary>
        /// Creates an identifier from the input string,
        /// without checking whether the string is valid.
        /// The caller should guarantee validity of the input.
        /// </summary>
        /// <param name="name">The identifier</param>
        static member MakeUnchecked (name : string) = String name

        /// <summary>
        /// Creates an identifier from the input string,
        /// </summary>
        /// <param name="name">The input identifier</param>
        static member Make (name : string) =
            if (is_identifier_valid name) = false then
                _logger.Error(sprintf "Invalid identifier: %s" name)
                raise (new YangModelException(sprintf "Invalid identifier: %s" name))
            else
                String name

        /// <summary>
        /// Gets the string value of the identifier
        /// </summary>
        member this.Value = let (String value) = this in value

        /// <summary>
        /// Checks whether the identifier has a valid name
        /// </summary>
        member this.IsValid = is_identifier_valid this.Value

        override this.ToString() = this.Value

    /// YANG Identifier with prefix
    [<StructuredFormatDisplay("{Value}")>]
    type IdentifierWithPrefix = {
        Prefix  : string
        Name    : string
    }
    with
        /// <summary>
        /// Creates a composite identifier without checking validity of input string;
        /// Caller should guarantee that the prefix and name are valid.
        /// </summary>
        /// <param name="prefix">The prefix of the identifier</param>
        /// <param name="name">The name of the identifier</param>
        static member MakeUnchecked (prefix, name) = { Prefix = prefix; Name = name }

        /// <summary>
        /// Creates a composite identifier
        /// </summary>
        /// <param name="prefix">The prefix of the identifier</param>
        /// <param name="name">The name of the identifier</param>
        static member Make (prefix, name) =
            if (is_identifier_valid prefix) = false then
                _logger.Error(sprintf "Invalid prefix for identifier: %s" prefix)
                raise (new YangModelException(sprintf "Invalid prefix for identifier: %s" prefix))
            if (is_identifier_valid name) = false then
                _logger.Error(sprintf "Invalid name of (prefixed) identifier: %s" name)
                raise (new YangModelException(sprintf "Invalid name of (prefixed) identifier : %s" name))
            { Prefix = prefix; Name = name }

        /// <summary>
        /// Gets the string value of the identifier
        /// </summary>
        member this.Value = sprintf "%s:%s" this.Prefix this.Name

        /// <summary>
        /// Checks whether the identifier has a valid name
        /// </summary>
        member this.IsValid = (is_identifier_valid this.Prefix) && (is_identifier_valid this.Name)

        override this.ToString() = this.Value

    /// Captures either a simple or custom identifier
    [<StructuredFormatDisplay("{Value}")>]
    type IdentifierReference =
    | Simple of Identifier
    | Custom of IdentifierWithPrefix
    with
        member this.Value =
            match this with
            | Simple identifier -> identifier.Value
            | Custom identifier -> identifier.Value

        override this.ToString() = this.Value

        member this.IsValid =
            match this with
            | Simple identifier -> identifier.IsValid
            | Custom identifier -> identifier.IsValid
