﻿// DefUseResolver.fs
// Collects and processes definition and use statements for types and groupings.
namespace Yang.Model

/// Methods for detecting and processing definition and use statements for types and groupings
module DefUseResolver =
    open System.Collections.Generic
    open StatementHelper.Patterns
    open System.Text.RegularExpressions

    // TODO: Deal with module prefixes
    // TODO: Deal with imports and includes

    let private default_path_separator = '/'

    type IdUse = IdentifierReference * (int option)
    type IdDef = IdentifierReference * int

    let private mkIdUse id = id, None
    let private mkIdDef id number = id, number

    [<StructuredFormatDisplay("{AsString}")>]
    type NodeType =
    | TypeUse               of IdUse
    | TypeDefinition        of IdDef
    | GroupingUse           of IdUse
    | GroupingDefinition    of IdDef
    with
        member this.AsString =
            match this with
            | TypeUse               (ty, None)          ->
                sprintf "type use: %s%s" ty.Value (if ty.IsPrimitive then "" else "[?]")
            | TypeUse               (ty, Some unique)   ->
                sprintf "type use: %s[%d]" ty.Value unique
            | TypeDefinition        (_, unique)         ->
                sprintf "type definition [%d]" unique
            | GroupingUse           (ty, None)          ->
                sprintf "grouping use: %s[?]" ty.Value
            | GroupingUse           (ty, Some unique)   ->
                sprintf "grouping use: %s[%d]" ty.Value unique
            | GroupingDefinition    (_, unique)         ->
                sprintf "grouping definition [%d]" unique

        override this.ToString() = this.AsString

        member this._IsTypeUse            = match this with | TypeUse            _ -> true | _ -> false
        member this._IsTypeDefinition     = match this with | TypeDefinition     _ -> true | _ -> false
        member this._IsGroupingUse        = match this with | GroupingUse        _ -> true | _ -> false
        member this._IsGroupingDefinition = match this with | GroupingDefinition _ -> true | _ -> false

        member this.AsTypeUse             = match this with | TypeUse            v -> Some v | _ -> None
        member this.AsTypeDefinition      = match this with | TypeDefinition     v -> Some v | _ -> None
        member this.AsGroupingUse         = match this with | GroupingUse        v -> Some v | _ -> None
        member this.AsGroupingDefinition  = match this with | GroupingDefinition v -> Some v | _ -> None

    module Patterns =
        let (|GroupingDef|_|) = function | GroupingDefinition x -> Some x | _ -> None
        let (|GroupingUse|_|) = function | GroupingUse        x -> Some x | _ -> None
        let (|TypeDef|_|)     = function | TypeDefinition     x -> Some x | _ -> None
        let (|TypeUse|_|)     = function | TypeUse            x -> Some x | _ -> None

        let (|GroupingDefOption|_|) : NodeType option -> IdDef option = function
        | Some (GroupingDef group)  -> Some group
        | _                         -> None

        let (|GroupingUseOption|_|) : NodeType option -> IdUse option = function
        | Some (GroupingUse group)  -> Some group
        | _                         -> None

        let (|TypeDefOption|_|) : NodeType option -> IdDef option = function
        | Some (TypeDefinition group)   -> Some group
        | _                             -> None

        let (|TypeUseOption|_|) : NodeType option -> IdUse option = function
        | Some (TypeUse group)      -> Some group
        | _                         -> None

        let (|GroupingUseUnresolved|_|) = function
        | (GroupingUse (id, None))      -> Some id
        | _                             -> None

        let (|GroupingUseResolved|_|) : NodeType -> IdDef option = function
        | (GroupingUse (id, Some seq))  -> Some (id, seq)
        | _                             -> None

        let (|GroupingUseUnresolvedOption|_|) : NodeType option -> IdentifierReference option = function
        | Some (GroupingUse (id, None)) -> Some id
        | _                             -> None

        let (|GroupingUseResolvedOption|_|) : NodeType option -> IdDef option = function
        | Some (GroupingUse (id, Some seq)) -> Some (id, seq)
        | _                             -> None

        let (|TypeUseUnresolved|_|) = function
        | (TypeUse (id, None))          -> Some id
        | _                             -> None

        let (|TypeUseUnresolvedOption|_|) = function
        | Some (TypeUse (id, None))     -> Some id
        | _                             -> None

        let (|TypeUseResolved|_|) : NodeType -> IdDef option = function
        | (TypeUse (id, Some seq))          -> Some (id, seq)
        | _                             -> None

        let (|TypeUseResolvedOption|_|) : NodeType option -> IdDef option = function
        | Some (TypeUse (id, Some seq))     -> Some (id, seq)
        | _                             -> None

        let IsTypeUse               = function | TypeUse            _ -> true | _ -> false
        let IsTypeDefinition        = function | TypeDefinition     _ -> true | _ -> false
        let IsGroupingUse           = function | GroupingUse        _ -> true | _ -> false
        let IsGroupingDefinition    = function | GroupingDefinition _ -> true | _ -> false

        let GetIdDef = function
        | TypeDefinition id
        | GroupingDefinition id
            -> Some id
        | _ -> None

    type Groupings =
        static member Use id                        = GroupingUse (id, None)
        static member Use (id : string)             = GroupingUse (IdentifierReference.Make id, None)
        static member Use ((id, sequence) : IdDef)  = GroupingUse (id, Some sequence)
        static member Use (id : string, sequence)   = GroupingUse (IdentifierReference.Make id, Some sequence)
        static member Use (node : NodeType)         =
            match node with
            | GroupingDefinition (id, sequence) -> GroupingUse (id, Some sequence)
            | _                                 -> failwith "Unexpected node type"

        static member Define (id, seq)              = GroupingDefinition (id, seq)
        static member Define (id : string, seq)     = GroupingDefinition (IdentifierReference.Make id, seq)

    type Types =
        static member Use id                        = TypeUse (id, None)
        static member Use (id : string)             = TypeUse (IdentifierReference.Make id, None)
        static member Use ((id, sequence) : IdDef)  = TypeUse (id, Some sequence)
        static member Use (id : string, sequence)   = TypeUse (IdentifierReference.Make id, Some sequence)
        static member Use (node : NodeType)         =
            match node with
            | TypeDefinition (id, sequence)     -> TypeUse (id, Some sequence)
            | _                                 -> failwith "Unexpected node type"

        static member Define (id, seq)              = TypeDefinition (id, seq)
        static member Define (id : string, seq)     = TypeDefinition (IdentifierReference.Make id, seq)

    [<StructuredFormatDisplay("{AsString}")>]
    type Path = | Path of (IdentifierReference list)
    with
        static member Empty = Path []
        static member Make (identifier : Identifier) =
            Path [ IdentifierReference.Make identifier ]
        static member Make (identifier : Identifier list) =
            Path ( identifier |> List.map IdentifierReference.Make)
        static member Make (identifier : IdentifierReference) =
            Path [ identifier ]
        static member Make (identifier : IdentifierReference list) =
            Path identifier
        static member Make (identifier : IdentifierWithPrefix) =
            Path [ IdentifierReference.Make identifier ]
        static member Make (identifier : IdentifierWithPrefix list) =
            Path (identifier |> List.map IdentifierReference.Make)
        static member Make (identifier : string) =
            Path [ IdentifierReference.Make identifier ]

        static member MakeFromPath (identifier : string, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let ids = identifier.Split(separator) |> Array.toList |> List.rev
            Path ( ids |> List.map IdentifierReference.Make )

        member this._Path = let (Path path) = this in path
        member this._Head = let (Path path) = this in List.head path

        member this.Push (identifier : Identifier) =
            Path ((IdentifierReference.Make identifier) :: this._Path)

        member this.Push (identifier : IdentifierReference) =
            Path (identifier :: this._Path)

        member this.Push (identifier : IdentifierWithPrefix) =
            Path ((IdentifierReference.Make identifier) :: this._Path)

        member this.Push (identifier : string) =
            Path ((IdentifierReference.Make identifier) :: this._Path)

        member this.Pop () =
            let rest = List.tail this._Path
            Path rest

        member this.Parent = this.Pop ()

        member this.AsPathList = this._Path |> List.rev

        member this.AsString =
            let path = this._Path |> List.rev |> List.map (fun p -> p.Value) |> String.concat "/"
            sprintf "/%s" path

        override this.ToString() = this.AsString

    [<StructuredFormatDisplay("{AsString}")>]
    type Node = | Node of Path:Path * Type:(NodeType option)
    with
        static member Make (identifier : Identifier, ``type``: NodeType) =
            Node (Path.Make identifier, Some ``type``)
        static member Make (identifier : string, ``type``: NodeType) =
            Node (Path.MakeFromPath identifier, Some ``type``)

        static member MakeGroupingDefinition(path : string, group_name : string, sequence : int, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let path = Path.MakeFromPath(path, separator)
            let path' = path.Push group_name
            Node (path', Some (Groupings.Define (group_name, sequence)))

        static member MakeGroupingUse(path : string, group_name : string, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let path = Path.MakeFromPath(path, separator)
            Node (path, Some (Groupings.Use group_name))
        static member MakeGroupingUse(path : string, group_name : string, sequence : int, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let path = Path.MakeFromPath(path, separator)
            Node (path, Some (Groupings.Use (group_name, sequence)))

        static member MakeTypeDefinition(path : string, type_name : string, sequence : int, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let path = Path.MakeFromPath(path, separator)
            let path' = path.Push type_name
            Node (path', Some (Types.Define (type_name, sequence)))

        static member MakeTypeUse(path : string, type_name : string, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let path = Path.MakeFromPath(path, separator)
            Node (path, Some (Types.Use type_name))
        static member MakeTypeUse(path : string, type_name : string, sequence : int, ?separator : char) =
            let separator = defaultArg separator default_path_separator
            let path = Path.MakeFromPath(path, separator)
            Node (path, Some (Types.Use (type_name, sequence)))
        static member MakeTypeUse(path : Path, id : IdentifierReference) =
            Node (path, Some (TypeUse (id, None)))
        static member MakeTypeUse(path : Path, id : IdentifierReference, sequence : int) =
            Node (path, Some (TypeUse (id, Some sequence)))
        static member MakeTypeUse(path : Path, id : IdentifierReference, sequence : int option) =
            Node (path, Some (TypeUse (id, sequence)))

        member this._Path = let (Node (path, _)) = this in path
        member this._Head = let (Node (path, _)) = this in path._Head
        member this._Type = let (Node (_, ``type``)) = this in ``type``

        member this.Push (identifier : Identifier, ``type`` : NodeType) =
            Node (this._Path.Push identifier, Some ``type``)

        member this.Push (identifier : IdentifierReference, ``type`` : NodeType) =
            Node (this._Path.Push identifier, Some ``type``)

        member this.Push (identifier : IdentifierWithPrefix, ``type`` : NodeType) =
            Node (this._Path.Push identifier, Some ``type``)

        member this.Push (identifier : Identifier) =
            Node (this._Path.Push identifier, None)

        member this.Push (identifier : IdentifierReference) =
            Node (this._Path.Push identifier, None)

        member this.Push (identifier : IdentifierWithPrefix) =
            Node (this._Path.Push identifier, None)

        member this.AsString =
            let (Node (path, ``type``)) = this
            match ``type`` with
            | None      -> sprintf "%s [-]" path.AsString
            | Some t    -> sprintf "%s [%s]" path.AsString t.AsString


    let VisitDefinitions (filter : Statement -> bool) (root : Statement) : Node list=
        // This version is much faster than using a work list, collection of results,
        // and tail recursion.

        let definitions = Dictionary<IdentifierReference, int>()
        let getUnique (id : IdentifierReference) =
            if definitions.ContainsKey(id) then
                let unique = definitions.[id] + 1
                definitions.[id] <- unique
                unique
            else
                definitions.Add(id, 1)
                1

        let mkTypeDefinition (id : Identifier) =
            let id = IdentifierReference.Make id
            let unique = getUnique id
            Some (TypeDefinition (mkIdDef id unique))

        let mkTypeUse (id : IdentifierReference) =
            Some (TypeUse (id, None))

        let mkGroupingDefinition (id : Identifier) =
            let id = IdentifierReference.Make id
            let unique = getUnique id
            Some (GroupingDefinition (id, unique))

        let mkGroupingUse (id : IdentifierReference) =
            Some (GroupingUse (id, None))

        let get (path : Path) (statement : Statement) : Node option =
            // printfn "Visiting %A" path
            match statement with
            | TypeDef (TypeDefStatement (id, _)) ->
                if filter statement then
                    let path' = path.Push id

                    Node (path', mkTypeDefinition id) |> Some
                else None
            | Type (TypeStatement (id, _)) ->
                if filter statement then
                    Node (path, mkTypeUse id) |> Some
                else None
            | Grouping (GroupingStatement (id, _)) ->
                if filter statement then
                    let path' = path.Push id
                    Node (path', mkGroupingDefinition id) |> Some
                else None
            | Uses (UsesStatement (id, _)) ->
                if filter statement then
                    Node (path, mkGroupingUse id) |> Some
                else None
            | _ -> None

        let rec find (path : Path) (statement : Statement) =
            if filter statement then
                match StatementHelper.GetReferenceIdentifier statement with
                | None  ->
                    // Statement without a label. These ones typically don't have sub-statements.
                    // Ignore them for now.
                    // TODO: Name resolution for statements without labels
                    []
                | Some id ->
                    let path' = path.Push id

                    let active = get path statement

                    let inner = StatementHelper.Children statement
                    if active.IsNone then
                        inner |> List.collect (
                            fun child ->
                                find path' child
                        )
                    else
                        active.Value :: (
                            inner |> List.collect (
                                fun child ->
                                    find path' child
                            )
                        )
            else
                []

        find Path.Empty root

