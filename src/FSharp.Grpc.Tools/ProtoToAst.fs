module FSharp.Grpc.Tools.ProtoToAst

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.FieldClass
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

/// Recursively convert a message to its record + companion module + sibling
/// oneof DUs. Nested types (messages and enums) live INSIDE the companion
/// module's `Types` submodule, mirroring the convention C#'s protobuf codegen
/// has used for years. The `Types` separator is what prevents F#'s field-vs-
/// module name resolution from picking record fields when chaining through
/// nested type paths (see issue #13 PR notes).
///
/// `currentMsgFqn` is the proto fully-qualified name of this message (e.g.
/// `.acme.platform.v1.Organization`), threaded so codegen can emit relative
/// `Types.X` references for own nested types and absolute `Parent.Types.X`
/// for cross-message refs.
let rec private messageToTypes (currentMsgFqn: string) (msg: DescriptorProto) =
    let classified = classifyFields msg

    // Generate DUs only for real oneofs. DUs live at parent scope (sibling
    // of the record), NOT inside the Types submodule.
    let oneofDUs =
        classified
        |> List.choose (function
            | OneofLeader(_, decl, fields) -> Some(UnionDef(oneofToUnion msg.Name decl fields))
            | _ -> None)

    let recordFields =
        classified
        |> List.map (function
            | OneofLeader(_, decl, _) ->
                let duName = oneofTypeName msg.Name decl.Name
                Field(toPascalCase decl.Name, LongIdent($"{duName} option"))
            | Scalar f
            | MapEntry(f, _)
            | Repeated f
            | Optional f
            | Message f -> Field(toPascalCase f.Name, resolveFieldType msg f))

    let record =
        RecordDef(
            Record(msg.Name) {
                for f in recordFields do
                    f
            }
        )

    // Build companion module with `empty` value.
    let emptyFields =
        classified
        |> List.map (function
            | OneofLeader(_, decl, _) ->
                let duName = oneofTypeName msg.Name decl.Name
                RecordFieldExpr(toPascalCase decl.Name, oneofDefaultExpr duName)
            | Scalar f
            | MapEntry(f, _)
            | Repeated f
            | Optional f
            | Message f -> RecordFieldExpr(toPascalCase f.Name, defaultValueExpr msg f))

    // Nested types (enums and messages) — recursively built so each carries
    // its own nested Types submodule chain.
    let nestedEnumDefs =
        msg.EnumType
        |> Seq.collect (fun e -> [ EnumDef(enumToEnum e); ModuleDef(enumJsonModule e) ])
        |> Seq.toList

    let nestedMessageDefs =
        msg.NestedType
        |> Seq.filter (not << isMapEntry)
        |> Seq.collect (fun n -> messageToTypes $"{currentMsgFqn}.{n.Name}" n)
        |> Seq.toList

    let typesContent = nestedEnumDefs @ nestedMessageDefs

    let computeSizeFunc = generateComputeSizeAST currentMsgFqn msg
    let writeToFunc = generateWriteToAST currentMsgFqn msg
    let encodeFunc = generateEncodeAST msg
    let decodeFromFunc = generateDecodeFromAST currentMsgFqn msg
    let decodeFunc = generateDecodeAST msg
    let writeJsonToFunc = generateWriteJsonToAST currentMsgFqn msg
    let encodeJsonFunc = generateEncodeJsonAST msg
    let decodeJsonElementFunc = generateDecodeJsonElementAST currentMsgFqn msg
    let decodeJsonFunc = generateDecodeJsonAST msg

    let companionModule =
        if List.isEmpty typesContent then
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
        else
            ModuleDef(
                Module(msg.Name) {
                    Module("Types") {
                        for t in typesContent do
                            match t with
                            | EnumDef e -> e
                            | RecordDef r -> r
                            | UnionDef u -> u
                            | ModuleDef m -> m
                    }

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
    // With the new nested layout, any message that has nested message/enum
    // types forces `rec` — the parent record forward-references its own
    // `Parent.Types.Inner`, which is declared later inside the companion
    // module. Single-walk: a real nested message (not a synthetic map entry),
    // a nested enum, or a recursive hit anywhere in the subtree.
    let rec hasAnyNestedType (msg: DescriptorProto) =
        not (Seq.isEmpty msg.EnumType)
        || (msg.NestedType |> Seq.exists (fun n -> not (isMapEntry n) || hasAnyNestedType n))

    if file.MessageType |> Seq.exists hasAnyNestedType then
        true
    else

        // No nested types in this file, but cross-message forward refs at the
        // top level can still require `rec`. Example:
        //     message Foo { Bar b = 1; }
        //     message Bar { string s = 1; }
        // Foo's body references Bar, declared after Foo — without `rec` this
        // doesn't compile. The position-based detection below covers it.

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
    let packageFqn =
        if System.String.IsNullOrEmpty file.Package then
            ""
        else
            "." + file.Package

    let enums =
        file.EnumType
        |> Seq.collect (fun e -> [ EnumDef(enumToEnum e); ModuleDef(enumJsonModule e) ])
        |> Seq.toList

    let messageTypes =
        file.MessageType
        |> Seq.collect (fun m -> messageToTypes $"{packageFqn}.{m.Name}" m)
        |> Seq.toList

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
