module FSharp.Grpc.Tools.ProtoToAst

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.WireFormat
open FSharp.Grpc.Tools.ComputeSizeGen
open FSharp.Grpc.Tools.WriteToGen
open FSharp.Grpc.Tools.DecodeGen
open FSharp.Grpc.Tools.JsonEncodeGen
open FSharp.Grpc.Tools.JsonDecodeGen

/// A generated F# type definition.
type private TypeDef =
    | EnumDef of WidgetBuilder<TypeDefnEnumNode>
    | RecordDef of WidgetBuilder<TypeDefnRecordNode>
    | UnionDef of WidgetBuilder<TypeDefnUnionNode>
    | ModuleDef of WidgetBuilder<NestedModuleNode>

/// Convert a DescriptorProto (message) to record + any oneof DUs.
let private messageToTypes (msg: DescriptorProto) =
    let mapEntries = mapEntryTypes msg

    // Group real oneof fields (not proto3 synthetic optional oneofs).
    let oneofFields =
        msg.Field
        |> Seq.filter (fun f -> f.HasOneofIndex && not f.Proto3Optional)
        |> Seq.groupBy (fun f -> f.OneofIndex)
        |> Map.ofSeq

    // Generate DUs only for real oneofs.
    let oneofDUs =
        msg.OneofDecl
        |> Seq.mapi (fun i decl ->
            let fields = oneofFields |> Map.tryFind i |> Option.defaultValue Seq.empty
            (i, decl, fields))
        |> Seq.filter (fun (i, _, _) -> oneofFields |> Map.containsKey i)
        |> Seq.map (fun (_, decl, fields) -> UnionDef(oneofToUnion msg.Name decl fields))
        |> Seq.toList

    // Track which oneof indices we've already emitted a record field for.
    let mutable emittedOneofs = Set.empty

    let recordFields =
        msg.Field
        |> Seq.sortBy (fun f -> f.Number)
        |> Seq.choose (fun f ->
            if f.HasOneofIndex && not f.Proto3Optional then
                let idx = f.OneofIndex

                if emittedOneofs |> Set.contains idx then
                    None
                else
                    emittedOneofs <- emittedOneofs |> Set.add idx
                    let decl = msg.OneofDecl.[idx]
                    let duName = oneofTypeName msg.Name decl.Name
                    Some(Field(toPascalCase decl.Name, LongIdent($"{duName} option")))
            elif mapEntries |> Set.contains (simpleTypeName f.TypeName) then
                // Map field: resolve via map entry.
                Some(Field(toPascalCase f.Name, resolveFieldType msg f))
            else
                Some(Field(toPascalCase f.Name, resolveFieldType msg f)))
        |> Seq.toList

    let record =
        RecordDef(
            Record(msg.Name) {
                for f in recordFields do
                    f
            }
        )

    // Build companion module with `empty` value.
    let mutable emittedOneofsForEmpty = Set.empty

    let emptyFields =
        msg.Field
        |> Seq.sortBy (fun f -> f.Number)
        |> Seq.choose (fun f ->
            if f.HasOneofIndex && not f.Proto3Optional then
                let idx = f.OneofIndex

                if emittedOneofsForEmpty |> Set.contains idx then
                    None
                else
                    emittedOneofsForEmpty <- emittedOneofsForEmpty |> Set.add idx
                    let decl = msg.OneofDecl.[idx]
                    let duName = oneofTypeName msg.Name decl.Name
                    Some(RecordFieldExpr(toPascalCase decl.Name, oneofDefaultExpr duName))
            else
                Some(RecordFieldExpr(toPascalCase f.Name, defaultValueExpr msg f)))
        |> Seq.toList

    let computeSizeFunc = generateComputeSizeAST msg
    let writeToFunc = generateWriteToAST msg
    let encodeFunc = generateEncodeAST msg
    let decodeFromFunc = generateDecodeFromAST msg
    let decodeFunc = generateDecodeAST msg
    let writeJsonToFunc = generateWriteJsonToAST msg
    let encodeJsonFunc = generateEncodeJsonAST msg
    let decodeJsonElementFunc = generateDecodeJsonElementAST msg
    let decodeJsonFunc = generateDecodeJsonAST msg

    let companionModule =
        ModuleDef(
            Module(msg.Name) {
                Value("empty", RecordExpr(emptyFields))
                computeSizeFunc
                writeToFunc
                encodeFunc
                decodeFromFunc
                decodeFunc
                writeJsonToFunc
                encodeJsonFunc
                decodeJsonElementFunc
                decodeJsonFunc
            }
        )

    oneofDUs @ [ record; companionModule ]

/// Recursively collect all type definitions from a message,
/// flattening nested types before the parent record.
/// Skips synthetic map entry types.
let rec private collectMessageTypes (msg: DescriptorProto) =
    let nestedEnums =
        msg.EnumType
        |> Seq.collect (fun e -> [ EnumDef(enumToEnum e); ModuleDef(enumJsonModule e) ])
        |> Seq.toList

    let nestedMessages =
        msg.NestedType
        |> Seq.filter (not << isMapEntry)
        |> Seq.collect collectMessageTypes
        |> Seq.toList

    nestedEnums @ nestedMessages @ messageToTypes msg

/// True if any message in the file references another in-file message or enum
/// that's declared *after* it in the generator's output order. Such forward
/// refs require `namespace rec` to compile — without `rec`, F# evaluates
/// declarations top-down and a later type/module isn't yet in scope.
///
/// Keyed on the fully-qualified proto name (e.g. `.example.Outer.Inner`) so
/// nested types with the same simple name in different parents don't collide.
/// Field `TypeName` is already FQN-shaped (protoc resolves it).
///
/// Order matches `generate`'s output: top-level enums first, then each top-level
/// message's subtree (per `collectMessageTypes`: nested enums, nested messages
/// recursively, then the message itself).
///
/// Self-references (e.g. `User.manager: User`) don't need `rec` — record types
/// are inherently self-recursive — so the `>` (strict) comparison excludes them.
let private needsRecursiveNamespace (file: FileDescriptorProto) : bool =
    let buildFqn (parentFqn: string) (name: string) =
        if parentFqn = "" then $".{name}" else $"{parentFqn}.{name}"

    let packageFqn =
        if System.String.IsNullOrEmpty file.Package then
            ""
        else
            "." + file.Package

    // Yield the FQN of every named type (enum or message) in the order it
    // appears in generator output.
    let rec messageSubtree (parentFqn: string) (msg: DescriptorProto) =
        seq {
            let myFqn = buildFqn parentFqn msg.Name

            for e in msg.EnumType do
                yield buildFqn myFqn e.Name

            for nested in msg.NestedType do
                if not (isMapEntry nested) then
                    yield! messageSubtree myFqn nested

            yield myFqn
        }

    let orderedFqns =
        seq {
            for e in file.EnumType do
                yield buildFqn packageFqn e.Name

            for m in file.MessageType do
                yield! messageSubtree packageFqn m
        }
        |> Seq.toList

    let positionByFqn = orderedFqns |> List.mapi (fun i fqn -> fqn, i) |> Map.ofList

    // Re-walk to pair each message with its FQN; check its field refs against
    // the position map.
    let rec messagesWithFqn (parentFqn: string) (msg: DescriptorProto) =
        seq {
            let myFqn = buildFqn parentFqn msg.Name

            for nested in msg.NestedType do
                if not (isMapEntry nested) then
                    yield! messagesWithFqn myFqn nested

            yield (myFqn, msg)
        }

    file.MessageType
    |> Seq.collect (messagesWithFqn packageFqn)
    |> Seq.exists (fun (myFqn, msg) ->
        let myPos = positionByFqn.[myFqn]

        msg.Field
        |> Seq.exists (fun f ->
            if
                f.Type = FieldDescriptorProto.Types.Type.Message
                || f.Type = FieldDescriptorProto.Types.Type.Enum
            then
                match Map.tryFind f.TypeName positionByFqn with
                | Some targetPos -> targetPos > myPos
                | None -> false
            else
                false))

/// Extract the package portion of a fully-qualified proto type name
/// (e.g. ".shared.Money" -> Some "Shared"; ".example.users.User" -> Some "Example.Users";
///  ".TopLevelType" -> None). Package segments are recognised by their
/// lowercase first letter; type segments start with an uppercase letter.
let private extractPackage (typeName: string) : string option =
    let parts = typeName.TrimStart('.').Split('.')

    let packageParts =
        parts |> Array.takeWhile (fun p -> p.Length > 0 && System.Char.IsLower p.[0])

    if packageParts.Length = 0 then
        None
    else
        Some(packageParts |> Array.map toPascalCase |> String.concat ".")

/// Walk every field of every message (including nested) and every service
/// method's input/output type, collecting the set of *other* F# namespaces
/// the generated file references. Used to emit `open` statements so
/// cross-package type references resolve.
///
/// Skipped:
///   - google.protobuf.* — WKT and wrapper types are emitted with their
///     fully-qualified F# names directly, so no `open` is needed.
///   - the file's own package — already in scope.
let private collectImportedPackages (file: FileDescriptorProto) : string list =
    let currentPackage = toNamespace file.Package

    let typeRefOf (f: FieldDescriptorProto) =
        if
            f.Type = FieldDescriptorProto.Types.Type.Message
            || f.Type = FieldDescriptorProto.Types.Type.Enum
        then
            Some f.TypeName
        else
            None

    let rec messageTypeNames (msg: DescriptorProto) =
        seq {
            for f in msg.Field do
                match typeRefOf f with
                | Some tn -> yield tn
                | None -> ()

            for nested in msg.NestedType do
                yield! messageTypeNames nested
        }

    let serviceTypeNames =
        seq {
            for svc in file.Service do
                for m in svc.Method do
                    yield m.InputType
                    yield m.OutputType
        }

    let allTypeRefs =
        Seq.append (file.MessageType |> Seq.collect messageTypeNames) serviceTypeNames

    allTypeRefs
    |> Seq.choose extractPackage
    |> Seq.filter (fun pkg -> not (pkg.StartsWith "Google.Protobuf"))
    |> Seq.filter (fun pkg -> Some pkg <> currentPackage)
    |> Seq.distinct
    |> Seq.sort
    |> Seq.toList

/// Converts a FileDescriptorProto into a Fabulous.AST Oak widget
/// that represents the generated F# source file.
let generate (file: FileDescriptorProto) : WidgetBuilder<Oak> =
    let enums =
        file.EnumType
        |> Seq.collect (fun e -> [ EnumDef(enumToEnum e); ModuleDef(enumJsonModule e) ])
        |> Seq.toList

    let messageTypes = file.MessageType |> Seq.collect collectMessageTypes |> Seq.toList

    let allTypes = enums @ messageTypes
    let needsRec = needsRecursiveNamespace file
    let imports = collectImportedPackages file

    match toNamespace file.Package with
    | Some ns ->
        let nsBlock =
            Namespace(ns) {
                for pkg in imports do
                    Open(pkg)

                for t in allTypes do
                    match t with
                    | EnumDef e -> e
                    | RecordDef r -> r
                    | UnionDef u -> u
                    | ModuleDef m -> m
            }

        let nsBlock = if needsRec then nsBlock.toRecursive () else nsBlock
        Oak() { nsBlock }
    | None ->
        // No proto `package` — default the F# wrapper to a namespace named
        // after the proto file (e.g. `no_package.proto` -> `namespace NoPackage`).
        // F# library files must begin with `namespace` or `module`; an
        // AnonymousModule isn't valid library code.
        let ns = System.IO.Path.GetFileNameWithoutExtension(file.Name) |> toPascalCase

        Oak() {
            Namespace(ns) {
                for pkg in imports do
                    Open(pkg)

                for t in allTypes do
                    match t with
                    | EnumDef e -> e
                    | RecordDef r -> r
                    | UnionDef u -> u
                    | ModuleDef m -> m
            }
        }
