﻿namespace Yang.Parser

/// Helper utilities for parsing
[<AutoOpen>]
module Utilities =
    open FParsec

    /// Apply the parser on the input, and return the result.
    /// Fail with exception, if parsing failed.
    let apply_parser parser input =
        match (run parser input) with
        | Success (result, _, _)    -> result
        | Failure (message, _, _)   -> failwith (sprintf "Parsing failed: %s" message)

    /// Checks the result of a parser, and backtracks if token is not valid
    let resultSatisfies predicate msg (p: Parser<_,_>) : Parser<_,_> =
        let error = messageError msg
        fun stream ->
          let state = stream.State
          let reply = p stream
          if reply.Status <> Ok || predicate reply.Result then reply
          else
              stream.BacktrackTo(state) // backtrack to beginning
              Reply(Error, error)

    /// Operator to aid the debugging of parsers
    let (<!>) (p: Parser<_,_>) label : Parser<_,_> =
        fun stream ->
            printfn "(Ln: %03d, Col: %03d): Entering %s" stream.Position.Line stream.Position.Column label
            let reply = p stream
            printfn "(Ln: %03d, Col: %03d): Leaving %s (%A)" stream.Position.Line stream.Position.Column label reply.Status
            reply

    let private pip_error_transform (errors : ErrorMessageList) =
        let pip_error = FParsec.ErrorMessage.ExpectedString("parser-in-parser: inner parser did not consume entire input string")
        if errors.Head <> null then
            match errors.Head with
            | :? FParsec.ErrorMessage.Expected as expected ->
                if expected.Label = "end of input" then
                    Reply(Error, ErrorMessageList(pip_error, errors))
                else Reply(Error, errors)
            | _ -> Reply(Error, errors)
        else Reply(Error, errors)

    /// Parser-in-parser: read a string with a string parser, and apply a second
    /// parser on the string read.
    let pip<'a, 'b> (outer : Parser<string, 'a>) (inner : Parser<'b, 'a>) =
        // TODO: Proper testing of parser-in-parser
        // TODO: Do we need to back-trace in parser-in-parser when failure to parse? If so, where?
        // TODO: Make sure that the inside parser in pip consumes the entire input provided by the first parser.
        fun (stream : CharStream<'a>) ->
            let state = stream.State
            let input = outer stream
            if input.Status = Ok then
                let str = input.Result
                let cs  = new CharStream<'a>(str, 0, str.Length)
                let output = (inner .>> eof) cs
                if output.Status = Ok then
                    Reply output.Result
                else
                    stream.BacktrackTo(state)
                    pip_error_transform output.Error
            else
                stream.BacktrackTo(state)
                Reply (Error, input.Error)

    /// Parser-in-parser and transform: read a string with a string parser, transform it,
    /// and apply the transformed string to a second parser.
    let pipt<'a, 'b> (outer : Parser<string, 'a>) (transform : string -> string) (inner : Parser<'b, 'a>) =
        // TODO: Proper testing of pipt
        // TODO: Do we need to back-trace in pipt when failure to parse? If so, where?
        // TODO: Make sure that the inside parser in pipt consumes the entire input provided by the first parser.
        fun (stream : CharStream<'a>) ->
            let state = stream.State
            let input = outer stream
            if input.Status = Ok then
                let str = transform input.Result
                let cs  = new CharStream<'a>(str, 0, str.Length)
                let output = (inner .>> eof) cs
                if output.Status = Ok then
                    Reply output.Result
                else
                    stream.BacktrackTo(state)
                    pip_error_transform output.Error
            else
                stream.BacktrackTo(state)
                Reply (Error, input.Error)

    /// Parses the next YANG string and checks whether it matches an expected value.
    /// It is similar to pstring, but works on YANG strings
    let pip_pstring<'a, 'b> (outer : Parser<string, 'a>) (expected : string) =
        // TODO: Proper testing of pip_pstring
        // TODO: Do we need to back-trace in pipt when failure to parse? If so, where?
        // TODO: Make sure that the inside parser in pipt consumes the entire input provided by the first parser.
        fun (stream : CharStream<'a>) ->
            let state = stream.State
            let input = outer stream
            if input.Status = Ok then
                let str = input.Result
                if str = expected then
                    Reply expected
                else
                    Reply (Error, ErrorMessageList (ErrorMessage.Expected expected))
            else
                stream.BacktrackTo(state)
                Reply (Error, input.Error)

    /// try_parse_sequence parser1 parser2: Apply parser1 then parser2. If either fails backtrack the stream.
    /// Both of the parsers will be tried, and the combined parser will succeed if either of parser1 or parser2
    /// succeeds.
    let try_parse_sequence<'a, 'T1, 'T2> (parser1 : Parser<'T1, 'a>) (parser2 : Parser<'T2, 'a>) : Parser<('T1 option) * ('T2 option), 'a> =
        fun stream ->
            let state = stream.State
            let reply1 = parser1 stream
            if reply1.Status = Ok then
                let state' = stream.State
                let reply2 = parser2 stream
                if reply2.Status = Ok then
                    let result = Some reply1.Result, Some reply2.Result
                    Reply result
                else
                    stream.BacktrackTo(state')
                    let result = Some reply1.Result, None
                    Reply result
            else
                stream.BacktrackTo(state)
                let reply2 = parser2 stream
                if reply2.Status = Ok then
                    let result = None, Some reply2.Result
                    Reply result
                else
                    stream.BacktrackTo(state)
                    Reply(Error, messageError "Neither parser managed to make progress")

    /// Helper methods on top of FParsec
    type ParserHelper =
        /// <summary>
        /// Applies a parser until it fails, and accumulates the state.
        /// It is similar to Intrinsic.Many, but it will not fail if the element parser fails.
        /// </summary>
        /// <param name="stateFromFirstElement">Construct starting state from first element</param>
        /// <param name="foldState">Update state based on element parsed</param>
        /// <param name="resultFromState">Constructs the final state to return at the end</param>
        /// <param name="elementParser">Parser of elements</param>
        /// <param name="firstElementParser">Parser for first element; if not defined, use the element parser</param>
        /// <param name="resultForEmptySequence">
        ///     Result from empty sequence;
        ///     if not defined use the default value for the output structure
        ///</param>
        static member inline ConsumeMany (  stateFromFirstElement: ('T -> 'State),
                                            foldState: ('State -> 'T -> 'State),
                                            resultFromState: ('State -> 'Result),
                                            elementParser: Parser<'T,'U>,
                                            ?firstElementParser: Parser<'T,'U>,
                                            ?resultForEmptySequence: (unit -> 'Result)
                                         ) : Parser<'Result,'U> =
            let firstElementParser = defaultArg firstElementParser elementParser
            let resultForEmptySequence = defaultArg resultForEmptySequence (fun _ -> Unchecked.defaultof<'Result>)

            fun stream ->
                let state = stream.State
                let reply = firstElementParser stream

                if reply.Status <> Ok then
                    stream.BacktrackTo(state)
                    Reply(resultForEmptySequence ())
                else
                    let mutable processingState : 'State = stateFromFirstElement reply.Result
                    let mutable processing = true

                    while processing do
                        let state' = stream.State
                        let reply' = elementParser stream

                        if reply'.Status <> Ok then
                            stream.BacktrackTo(state')
                            processing <- false
                        else
                            processingState <- foldState processingState reply'.Result

                    Reply(resultFromState processingState)
