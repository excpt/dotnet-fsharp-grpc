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

/// Converts a FileDescriptorProto into a Fabulous.AST Oak widget
/// that represents the generated F# source file.
let generate (file: FileDescriptorProto) : WidgetBuilder<Oak> =
    let enums =
        file.EnumType
        |> Seq.collect (fun e -> [ EnumDef(enumToEnum e); ModuleDef(enumJsonModule e) ])
        |> Seq.toList

    let messageTypes = file.MessageType |> Seq.collect collectMessageTypes |> Seq.toList

    let allTypes = enums @ messageTypes

    match toNamespace file.Package with
    | Some ns ->
        Oak() {
            Namespace(ns) {
                for t in allTypes do
                    match t with
                    | EnumDef e -> e
                    | RecordDef r -> r
                    | UnionDef u -> u
                    | ModuleDef m -> m
            }
        }
    | None ->
        Oak() {
            AnonymousModule() {
                for t in allTypes do
                    match t with
                    | EnumDef e -> e
                    | RecordDef r -> r
                    | UnionDef u -> u
                    | ModuleDef m -> m
            }
        }
