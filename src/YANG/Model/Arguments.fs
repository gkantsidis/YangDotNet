// Arguments.fs
// Basic tokens of the YANG model

namespace Yang.Model

module Arguments =
    open System
    open System.Numerics
    open NLog

    /// Logger for this module
    let private _logger = LogManager.GetCurrentClassLogger()

    let private throw fmt =
        let do_throw (message : string) =
            _logger.Error message
            raise (YangModelException message)
        Printf.ksprintf do_throw fmt

#if INTERACTIVE
    // The following are used only in interactive (fsi) to help with enabling disabling
    // logging for particular modules.

    type internal Marker = interface end
    let _full_name = typeof<Marker>.DeclaringType.FullName
    let _name = typeof<Marker>.DeclaringType.Name
#endif

    // Below we define a custom date field. We could have used the system DateTime,
    // but that gives more information (time) that specified by the grammar.

    let [<Literal>] private date_pattern = "(?<Year>[1-2][0-9]{3})\-(?<Month>(0[1-9]|1[0-2]))\-(?<Day>([0-2][0-9]|3[0-1]))"
    let private date_re = Text.RegularExpressions.Regex(date_pattern, Text.RegularExpressions.RegexOptions.Compiled)

    /// Represents a date (date-arg in YANG).
    [<StructuredFormatDisplay("{Value}")>]
    [<CustomEquality; CustomComparison>]
    type Date = {
        Year    : uint16
        Month   : uint8
        Day     : uint8
    } with
        static member Make(dt : DateTime) = {
            Year    = uint16 dt.Year
            Month   = uint8 dt.Month
            Day     = uint8 dt.Day
        }

        static member Make(year : uint16, month : uint8, day : uint8) =
            // Check that the date is valid; the following throws an exception if not valid
            let _ = DateTime(int year, int month, int day)
            {
                Year    = year
                Month   = month
                Day     = day
            }

        static member Make(year : int, month : int, day : int) = Date.Make (uint16 year, uint8 month, uint8 day)

        static member Make(date : string) =
            if date_re.IsMatch(date) then
                let result = date_re.Match(date)
                let year = result.Groups.Item("Year").Value |> Int32.Parse
                let month = result.Groups.Item("Month").Value |> Int32.Parse
                let day = result.Groups.Item("Day").Value |> Int32.Parse
                let date = Date.Make(year, month, day)
                date
            else invalidArg "date" "input is not a valid date string"

        member this.Value = sprintf "%04d-%02d-%02d" this.Year this.Month this.Day
        override this.ToString() = this.Value

        member this.AsDateTime = DateTime(int this.Year, int this.Month, int this.Day)

        interface IEquatable<Date> with
            member this.Equals other =
                this.Year   = other.Year &&
                this.Month  = other.Month &&
                this.Day    = other.Day

        override this.Equals other =
            match other with
            | :? Date as other' -> (this :> IEquatable<Date>).Equals(other)
            | _                 -> invalidArg "other" "cannot compare values of different types"

        override this.GetHashCode() =
            this.Year.GetHashCode() ^^^ this.Month.GetHashCode() ^^^ this.Day.GetHashCode()

        interface IComparable<Date> with
            member this.CompareTo other =
                if this.Year <> other.Year then
                    this.Year.CompareTo(other.Year)
                elif this.Month <> other.Month then
                    this.Month.CompareTo(other.Month)
                else
                    this.Day.CompareTo(other.Day)

        interface IComparable with
            member this.CompareTo other =
                match other with
                | :? Date as other' -> (this :> IComparable<Date>).CompareTo(other')
                | _                 -> invalidArg "other" "cannot compare values of different types"


    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Key = Key of IdentifierReference list
    with
        static member Make (keys : string) =
            if String.IsNullOrWhiteSpace(keys) then invalidArg "keys" "keys cannot be null or whitespace"
            let keys = keys.Split([| ' '; '\t'; '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                       |> Array.map (fun key -> key.Trim())
                       |> Array.toList
            Key (keys |> List.map (IdentifierReference.Make))

        member this.Value =
            let (Key keys) = this
            keys |> List.map (fun k -> k.Value) |> String.concat " "

        override this.ToString() = this.Value

    let KeyFromString (keys : string) = Key.Make keys

    [<AutoOpen>]
    module Length =
        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type LengthBoundary =
        | Min
        | Max
        | Number of uint64
        with
            // TODO: Add comparison support for LengthBoundary

            member this.Value =
                match this with
                | Min       -> "min"
                | Max       -> "max"
                | Number v  -> sprintf "%d" v

            member this._IsMin = match this with | Min -> true | _ -> false
            member this._IsMax = match this with | Max -> true | _ -> false
            member this._IsNumber = match this with | Number _ -> true | _ -> false

            member this.AsNumber =
                match this with
                | Number number     -> Some number
                | _                 -> None

            override this.ToString() = this.Value

        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type LengthPart =
        | Single of LengthBoundary
        | Range  of LengthBoundary * LengthBoundary
        with
            static member Make(value : uint64) =
                Single (Number value)

            static member Make(left, right) =
                // TODO: Check that the left boundary is smaller than the right
                Range (Number left, Number right)

            member this.Value =
                match this with
                | Single boundary       -> boundary.Value
                | Range (left, right)   -> sprintf "%s .. %s" left.Value right.Value

            override this.ToString() = this.Value

            member this._IsSingle = match this with | Single _ -> true | _ -> false
            member this._IsRange  = match this with | Range _  -> true | _ -> false

            member this.AsSingle =
                match this with
                | Single boundary   -> Some boundary
                | _                 -> None

            member this.AsRange =
                match this with
                | Range (left, right)   -> Some (left, right)
                | _                     -> None

            member this.IsInRange value =
                match this with
                | Single (Min _)
                | Single (Max _)        -> false
                | Single (Number v)     -> v = value
                | Range (left, right)   ->
                    match left, right with
                    | _, Min
                    | Max, _        -> throw "Invalid length range: %s .. %s" left.Value right.Value
                    | Min, Max      ->
                        _logger.Warn("Detected trivial range that accepts all lengths")
                        true
                    | Min, Number v -> value <= v
                    | Number v, Max -> value >= v
                    | Number left, Number right     -> left <= value && value <= right

        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type Length = | Length of LengthPart list
        with
            member this.Value = let (Length v) = this in v |> List.map (fun v' -> v'.Value) |> String.concat " | "
            override this.ToString() = this.Value

            member this.IsInRange value =
                let (Length v) = this
                v |> List.exists (fun part -> part.IsInRange value)


    /// Captures the max-value-arg from [RFC 7950, p. 192]
    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type MaxValue =
    | Unbounded
    | Bounded   of uint64
    with
        static member Make (bound : uint64) =
            if bound = 0uL then throw "MaxValue cannot be zero"
            else Bounded bound

        static member Make (bound : uint32) =
            if bound = 0ul then throw "MaxValue cannot be zero"
            else Bounded (uint64 bound)

        static member Make (bound : int32) =
            if bound <= 0 then throw "MaxValue cannot be zero or negative"
            else Bounded (uint64 bound)

        static member Make (bound : decimal) =
            if bound <= 0M then throw "MaxValue cannot be zero or negative"
            elif bound > (decimal UInt64.MaxValue) then throw "MaxValue cannot be greater than %d" UInt64.MaxValue
            else Bounded (uint64 bound)

        member this.Value =
            match this with
            | Unbounded     -> "unbounded"
            | Bounded v     -> sprintf "%d" v

        override this.ToString() = this.Value


    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Modifier =
    | InvertMatch
    with
        member this.Value = "invert-match"
        override this.ToString() = this.Value

    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type MinValue = MinValue of uint32
    with
        static member Make (value : uint32) = MinValue value
        static member Make (value : int32) =
            if value < 0 then throw "MinValue cannot be negative"
            else MinValue (uint32 value)
        static member Make (value : decimal) =
            if value < (decimal 0) then throw "MinValue cannot be negative"
            elif (value > (decimal UInt32.MaxValue)) then throw "MinValue cannot be greater than %d" UInt32.MaxValue
            else MinValue (uint32 value)

        member this.Value = let (MinValue mv) = this in sprintf "%d" mv
        override this.ToString() = this.Value

    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type OrderedBy =
    | User
    | System
    with
        member this.Value =
            match this with
            | User      -> "user"
            | System    -> "system"
        override this.ToString() = this.Value

    /// Captures the path-arg statements from [RFC 7950, p. 205-206]
    [<AutoOpen>]
    module Path =
        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type PathKey = PathKey of Up:uint16 * Node:(IdentifierReference list)
        with
            member this.IsValid =
                let (PathKey (up, nodes)) = this
                up > 0us && (List.length nodes > 0) && (nodes |> List.forall (fun node -> node.IsValid))

            member this.Value =
                let (PathKey (up, nodes)) = this
                let up_string = if up > 0us then String.replicate (int up) "../" else "/"
                let nodes_string =
                    if nodes.Length = 0 then ""
                    else nodes |> List.map (fun n -> n.Value) |> String.concat "/"
                sprintf "%s%s" up_string nodes_string

            override this.ToString() = this.Value

            member this._Up = let (PathKey (up, _)) = this in up
            member this._Node = let (PathKey (_, node)) = this in node

        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type PathPredicate = PathPredicate of Node:IdentifierReference * PathKey:PathKey
        with
            member this.Value =
                let (PathPredicate (node, key)) = this
                sprintf "%s = current()/%s" node.Value key.Value

            override this.ToString() = this.Value

            member this._Node = let (PathPredicate (node, _)) = this in node
            member this._Key = let (PathPredicate (_, key)) = this in key

        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type PathItem = PathItem of Node:IdentifierReference * Predicate:(PathPredicate list option)
        with
            static member Make (identifier : string) =
                PathItem (IdentifierReference.Make identifier, None)

            member this.Value =
                let (PathItem (node, predicate)) = this
                match predicate with
                | None              -> sprintf "%s" node.Value
                | Some predicate    ->
                    let predicates = predicate |> List.map (fun p -> sprintf "[%s]" p.Value)
                    sprintf "%s%s" node.Value (String.concat "" predicates)

            member this._Predicate = let (PathItem (_, predicate)) = this in predicate

            member this.HasPredicate =
                match this._Predicate with
                | None
                | Some []   -> false
                | _         -> true

            override this.ToString() = this.Value

        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type AbsolutePath = AbsolutePath of PathItem list
        with
            static member Make (identifier : string) =
                AbsolutePath [ PathItem (IdentifierReference.Make identifier, None) ]
            static member Make (identifier : string list) =
                let ids = identifier |> List.map IdentifierReference.Make
                let nodes = ids |> List.map (fun n -> PathItem (n, None))
                AbsolutePath nodes

            member private this.Raw = let (AbsolutePath path) = this in path
            member this.IsValid = this.Raw.Length > 0
            member this.Value = sprintf "/%s" (this.Raw |> List.map (fun node -> node.Value) |> String.concat "/")
            override this.ToString() = this.Value

            member this.Path = let (AbsolutePath path) = this in path

            member this.Append (identifier : string, ?predicate : PathPredicate) =
                let predicate =
                    match predicate with
                    | None      -> None
                    | Some p    -> Some [p]
                let item = PathItem (IdentifierReference.Make identifier, predicate)
                AbsolutePath (this.Raw @ [item])

            member this.Append (identifier : string, ?predicate : PathPredicate list) =
                let item = PathItem (IdentifierReference.Make identifier, predicate)
                AbsolutePath (this.Raw @ [item])

            member this.Append (identifier : IdentifierReference, ?predicate : PathPredicate) =
                let predicate =
                    match predicate with
                    | None      -> None
                    | Some p    -> Some [p]
                let item = PathItem (identifier, predicate)
                AbsolutePath (this.Raw @ [item])

            member this.Append (identifier : IdentifierReference, ?predicate : PathPredicate list) =
                let item = PathItem (identifier, predicate)
                AbsolutePath (this.Raw @ [item])

        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type RelativePath = RelativePath of Up:uint16 * PathItem list
        with
            static member Make (identifier : string, ?up : uint16) =
                let up = defaultArg up 1us
                let path = [ PathItem (IdentifierReference.Make identifier, None) ]
                RelativePath (up, path)

            static member Make (identifier : string list, ?up : uint16) =
                let up = defaultArg up 1us
                let ids = identifier |> List.map IdentifierReference.Make
                let nodes = ids |> List.map (fun n -> PathItem (n, None))
                RelativePath (up, nodes)

            member this.Path = let (RelativePath (_, path)) = this in path
            member this.UpSteps   = let (RelativePath (up, _))   = this in up

            member this.IsValid = this.Path.Length > 0 && this.UpSteps > 0us
            member this.Value =
                sprintf "%s%s"
                    (String.replicate (int this.UpSteps) "../")
                    (this.Path |> List.map (fun node -> node.Value) |> String.concat "/")

            override this.ToString() = this.Value

            member this.Append (identifier : string, ?predicate : PathPredicate) =
                let predicate =
                    match predicate with
                    | None      -> None
                    | Some p    -> Some [p]
                let item = PathItem (IdentifierReference.Make identifier, predicate)
                RelativePath (this.UpSteps, (this.Path @ [item]))

            member this.Append (identifier : string, ?predicate : PathPredicate list) =
                let item = PathItem (IdentifierReference.Make identifier, predicate)
                RelativePath (this.UpSteps, (this.Path @ [item]))

            member this.Append (identifier : IdentifierReference, ?predicate : PathPredicate) =
                let predicate =
                    match predicate with
                    | None      -> None
                    | Some p    -> Some [p]
                let item = PathItem (identifier, predicate)
                RelativePath (this.UpSteps, this.Path @ [item])

            member this.Append (identifier : IdentifierReference, ?predicate : PathPredicate list) =
                let item = PathItem (identifier, predicate)
                RelativePath (this.UpSteps, this.Path @ [item])


        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type Path =
        | Absolute of Absolute: AbsolutePath
        | Relative of Relative: RelativePath
        with
            member this.Value =
                match this with
                | Absolute path -> path.Value
                | Relative path -> path.Value
            override this.ToString() = this.Value

            member this._IsAbsolute = match this with | Absolute _ -> true | _ -> false
            member this._IsRelative = match this with | Relative _ -> true | _ -> false
            member this.AsAbsolute = match this with | Absolute v -> Some v | _ -> None
            member this.AsRelative = match this with | Relative v -> Some v | _ -> None


    /// Definition of Range ([RFC 7950, p. 204])
    [<AutoOpen>]
    module Range =
        open System.Windows.Markup

        [<StructuredFormatDisplay("{Value}")>]
        [<CustomEquality; CustomComparison>]
        [<Struct>]
        type RangeBoundary =
        | Min
        | Max
        | Integer of Integer:   BigInteger
        | Decimal of Decimal:   System.Decimal
        with
            static member Make (value : int64) = Integer (BigInteger value)
            static member Make (value : int32) = Integer (BigInteger value)
            static member Make (value : uint64) = Integer (BigInteger value)
            static member Make (value : uint32) = Integer (BigInteger value)
            static member Make (value : System.Decimal) = Decimal value

            member this.Value =
                match this with
                | Min           -> "min"
                | Max           -> "max"
                | Integer v     -> sprintf "%s" (v.ToString())
                | Decimal v     -> sprintf "%f" v

            override this.ToString() = this.Value

            member this._IsMin = match this with | Min -> true | _ -> false
            member this._IsMax = match this with | Max -> true | _ -> false
            member this._IsInteger = match this with | Integer _ -> true | _ -> false
            member this._IsDecimal = match this with | Decimal _ -> true | _ -> false

            member this.AsInteger =
                match this with
                | Integer value ->
                    if value > (BigInteger System.Int64.MaxValue) then
                        throw "Conversion to int64 failed; value too big"
                    elif value < (BigInteger System.Int64.MinValue) then
                        throw "Conversion to int64 failed; value too small"
                    else Some (int64 value)
                | _             -> None

            member this.AsUnsignedInteger =
                match this with
                | Integer value ->
                    if value > (BigInteger System.UInt64.MaxValue) then
                        throw "Conversion to int64 failed; value too big"
                    elif value < (BigInteger 0) then
                        throw "Cannot convert negative value to uint64"
                    else Some (uint64 value)
                | _             -> None

            member this.AsBigInteger =
                match this with
                | Integer value -> Some value
                | _             -> None

            member this.AsDecimal =
                match this with
                | Decimal value -> Some value
                | _             -> None

            interface IEquatable<RangeBoundary> with
                member this.Equals(other : RangeBoundary) =
                    match this, other with
                    | Min, Min
                    | Max, Max
                        -> true

                    | Min, _
                    | _, Min
                    | _, Max
                    | Max, _
                        -> false

                    | Integer v1, Integer v2    -> v1 = v2
                    | Decimal v1, Decimal v2    -> v1 = v2
                    | Integer v1, Decimal v2
                    | Decimal v2, Integer v1    ->
                        if    v1 > (BigInteger Decimal.MaxValue)
                           || v1 < (BigInteger Decimal.MinValue)
                        then false
                        else v2 = (decimal v1)

            interface IComparable<RangeBoundary> with
                member this.CompareTo(other : RangeBoundary) =
                    match this, other with
                    | Min, Min
                    | Max, Max
                        -> 0

                    | Min, _
                    | _, Max
                        -> -1

                    | _, Min
                    | Max, _
                        -> 1

                    | Integer v1, Integer v2    -> v1.CompareTo(v2)
                    | Decimal v1, Decimal v2    -> v1.CompareTo(v2)

                    | Integer v1, Decimal v2 ->
                        if v1 > (BigInteger Decimal.MaxValue) then 1
                        elif v1 < (BigInteger Decimal.MinValue) then -1
                        else
                            (decimal v1).CompareTo(v2)

                    | Decimal v2, Integer v1 ->
                        if v1 > (BigInteger Decimal.MaxValue) then -1
                        elif v1 < (BigInteger Decimal.MinValue) then 1
                        else
                            v2.CompareTo(decimal v1)


        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type RangePart =
        | Single    of RangeBoundary
        | Region    of RangeBoundary * RangeBoundary
        with
            static member Make(value : int64) = Single (RangeBoundary.Make value)
            static member Make(value : int32) = Single (RangeBoundary.Make value)
            static member Make(value : Decimal) = Single (RangeBoundary.Make value)

            static member Make(left : int64, right : int64) =
                if left > right then throw "Invalid range: %d .. %d" left right
                if left = right then Single (RangeBoundary.Make left)
                else Region (RangeBoundary.Make left, RangeBoundary.Make right)

            static member Make(left : int32, right : int32) =
                if left > right then throw "Invalid range: %d .. %d" left right
                if left = right then Single (RangeBoundary.Make left)
                else Region (RangeBoundary.Make left, RangeBoundary.Make right)

            static member Make(left : Decimal, right : Decimal) =
                if left > right then throw "Invalid range: %f .. %f" left right
                if left = right then Single (RangeBoundary.Make left)
                else Region (RangeBoundary.Make left, RangeBoundary.Make right)

            member this.Value =
                match this with
                | Single boundary       -> boundary.Value
                | Region (left, right)  -> sprintf "%s .. %s" left.Value right.Value

            override this.ToString() = this.Value

            member this._IsSingle =
                match this with
                | Single _ -> true
                | Region _ -> false

            member this._IsRegion =
                match this with
                | Single _ -> false
                | Region _ -> true

            member this.AsSingle =
                match this with
                | Single boundary   -> Some boundary
                | _                 -> None

            member this.AsRegion =
                match this with
                | Single _              -> None
                | Region (left, right)  -> Some (left, right)

            member this.IsInRange (value : int64) =
                let value' = BigInteger value

                match this with
                | Single Min
                | Single Max    -> false
                | Single (Integer v)    -> value' = v
                | Single (Decimal v)    -> (System.Decimal value) = v
                | Region (_, Min)
                | Region (Max, _)       -> throw "Invalid range-part: %A" this
                | Region (Min, Max)     ->
                    _logger.Warn("Detected trivial range part (min .. max)")
                    true
                | Region (Min, Integer v)   -> value' <= v
                | Region (Min, Decimal v)   -> (System.Decimal value) <= v
                | Region (Integer v, Max)   -> v <= value'
                | Region (Decimal v, Max)   -> v <= (System.Decimal value)
                | Region (Integer left, Integer right) ->
                    left <= value' && value' <= right
                | Region (Decimal left, Decimal right) ->
                    let value' = System.Decimal value
                    left <= value' && value' <= right
                | Region (Integer left, Decimal right) ->
                    let value'' = System.Decimal value
                    left <= value' && value'' <= right
                | Region (Decimal left, Integer right) ->
                    let value'' = System.Decimal value
                    left <= value'' && value' <= right

            member this.IsInRange (value : int32) = this.IsInRange (int64 value)

            member this.IsInRange (value : Decimal) =
                match this with
                | Single Min
                | Single Max    -> false
                | Single (Integer v)    ->
                    if    v > (BigInteger Decimal.MaxValue)
                       || v < (BigInteger Decimal.MinValue)
                    then false
                    else value = (decimal v)
                | Single (Decimal v)    -> value = v
                | Region (_, Min)
                | Region (Max, _)       -> throw "Invalid range-part: %A" this
                | Region (Min, Max)     ->
                    _logger.Warn("Detected trivial range part (min .. max)")
                    true
                | Region (Min, Integer v)   ->
                    if v > (BigInteger Decimal.MaxValue) then true
                    else value <= (decimal v)
                | Region (Min, Decimal v)   -> value <= v
                | Region (Integer v, Max)   ->
                    if v > (BigInteger Decimal.MaxValue) then false
                    else (decimal v) <= value
                | Region (Decimal v, Max)   -> v <= value
                | Region (Integer left, Integer right) ->
                    if    left > (BigInteger Decimal.MaxValue)
                       || right < (BigInteger Decimal.MinValue)
                    then false
                    else
                        (decimal left) <= value && value <= (decimal right)
                | Region (Decimal left, Decimal right) ->
                    left <= value && value <= right
                | Region (Integer left, Decimal right) ->
                    // We assume that left <= right
                    if left < (BigInteger Decimal.MinValue) then true
                    else (decimal left) <= value && value <= right
                | Region (Decimal left, Integer right) ->
                    // We assume that left <= right
                    if right > (BigInteger Decimal.MaxValue) then true
                    else left <= value && value <= (decimal right)

            member this.IsValid =
                match this with
                | Single _  -> true
                | Region (left, right) ->
                    (left :> IComparable<RangeBoundary>).CompareTo(right) <= 0


        /// Definition of Range ([RFC 7950, p. 204])
        [<StructuredFormatDisplay("{Value}")>]
        [<Struct>]
        type Range = Range of RangePart list
        with
            static member Make (left : BigInteger, right : BigInteger) =
                if left > right then throw "Left region cannot be larger than right (%s .. %s)" (left.ToString()) (right.ToString())
                if left = right then
                    Range [ Single (Integer left) ]
                else
                    Range [ Region (Integer left, Integer right) ]

            static member Make (left : int64, right : int64) =
                Range.Make (BigInteger left, BigInteger right)

            static member Make (left : int32, right : int32) =
                Range.Make (BigInteger left, BigInteger right)

            static member Make (left : System.Decimal, right : System.Decimal) =
                if left > right then throw "Left region cannot be larger than right (%f .. %f)" left right
                if left = right then
                    Range [ Single (Decimal left) ]
                else
                    Range [ Region (Decimal left, Decimal right) ]

            member this.Value = let (Range range) = this in range |> List.map (fun part -> part.Value) |> String.concat " | "
            override this.ToString() = this.Value

            member this.IsInRange (value : int64) =
                let (Range range) = this
                range |> List.exists (fun part -> part.IsInRange value)

            member this.IsInRange (value : int32) =
                let (Range range) = this
                range |> List.exists (fun part -> part.IsInRange value)

            member this.IsInRange (value : Decimal) =
                let (Range range) = this
                range |> List.exists (fun part -> part.IsInRange value)

            member this.IsValid =
                // TODO: Implement unit tests for validity check for range, see [RFC 7950, p. 147]
                let (Range ranges) = this
                let is_valid, _ =
                    ranges |> List.fold (
                        fun (valid, minimum) range ->
                            if valid = false            then (false, None)
                            elif range.IsValid = false  then (false, None)
                            else
                                match range with
                                | Single r ->
                                    if minimum.IsNone then (true, Some r)
                                    elif (minimum.Value :> IComparable<RangeBoundary>).CompareTo(r) < 0 then (true, Some r)
                                    else (false, None)
                                | Region (left, right) ->
                                    // We have already checked that the range is valid above.
                                    if (minimum.Value :> IComparable<RangeBoundary>).CompareTo(left) < 0 then (true, Some right)
                                    else (false, None)
                    ) (true, None)

                is_valid

            member this.Append (part : RangePart) =
                // TODO: Implement range narrowing, see [RFC 7950, p. 147]
                let (Range ranges) = this
                throw "Range narrowing not implemented"


        let MakeInt32Range (left : int32, right : int32) = Range.Make (left, right)


    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Status =
    | Current
    | Obsolete
    | Deprecated
    with
        member this.Value =
            match this with
            | Current       -> "current"
            | Obsolete      -> "obsolete"
            | Deprecated    -> "deprecated"

        override this.ToString() = this.Value

    /// Set of descendant schema nodes that specify uniqueness
    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Unique = Unique of SchemaNodeIdentifier list
    with
        static member Make (schema : SchemaNodeIdentifier list) =
            if (List.length schema) = 0 then
                throw "Cannot create a unique-arg from an empty list"
            if (schema |> List.exists (fun s -> s.IsAbsolute)) then
                throw "Cannot create a unique-arg from absolute paths"

            Unique schema

        member this.Value =
            let (Unique schema) = this
            schema |> List.map (fun i -> i.Value) |> String.concat " "
        override this.ToString() = this.Value

    let MakeUnique = Unique.Make
    let MakeUniqueDescendant (schema : SchemaNodeIdentifier list ) =
        if (schema |> List.exists (fun s -> s.IsAbsolute)) then
            throw "Did not expect absolute path when creating a unique arg"
        MakeUnique schema


    /// Captures the 'augment-arg' definition ([RFC 7950, p. 199])
    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Augment = Augment of SchemaNodeIdentifier
    with
        member this.Id = let (Augment id) = this in id
        member this.IsAbsolute   = this.Id.IsAbsolute
        member this.IsDescendant = this.Id.IsDescendant
        member this._Schema      = this.Id._Schema
        member this.Value = this.Id.Value
        override this.ToString() = this.Value

    /// Captures the 'deviation-arg' definition ([RFC 7950, p. 201])
    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Deviation = Deviation of SchemaNodeIdentifier
    with
        member this.Id = let (Deviation id) = this in id
        member this.IsAbsolute   = this.Id.IsAbsolute
        member this.IsDescendant = this.Id.IsDescendant
        member this._Schema      = this.Id._Schema
        member this.Value = this.Id.Value
        override this.ToString() = this.Value

    /// Captures the 'refine-arg' definition ([RFC 7950, p. 198])
    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type Refine = Refine of SchemaNodeIdentifier
    with
        member this.Id = let (Refine id) = this in id
        member this.IsAbsolute   = this.Id.IsAbsolute
        member this.IsDescendant = this.Id.IsDescendant
        member this._Schema      = this.Id._Schema
        member this.Value = this.Id.Value
        override this.ToString() = this.Value

    let MakeRefine id = Refine id

    /// Captures the 'uses-augment-arg' definition ([RFC 7950, p. 198])
    [<StructuredFormatDisplay("{Value}")>]
    [<Struct>]
    type UsesAugment = UsesAugment of SchemaNodeIdentifier
    with
        member this.Id = let (UsesAugment id) = this in id
        member this.IsAbsolute   = this.Id.IsAbsolute
        member this.IsDescendant = this.Id.IsDescendant
        member this._Schema      = this.Id._Schema
        member this.Value = this.Id.Value
        override this.ToString() = this.Value

    let BoolAsString v = if v then "true" else "false"

