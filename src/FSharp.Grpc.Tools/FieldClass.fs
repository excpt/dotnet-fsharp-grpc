module internal FSharp.Grpc.Tools.FieldClass

open Google.Protobuf.Reflection
open FSharp.Grpc.Tools.TypeMapping

/// The semantic category a proto field falls into when generating F# code.
/// Generators consume `classifyFields` instead of re-implementing the cascade
/// of oneof-dedup + map detection + label/optional/message/scalar checks.
type FieldKind =
    | Scalar of FieldDescriptorProto
    | OneofLeader of idx: int * decl: OneofDescriptorProto * fields: FieldDescriptorProto list
    | MapEntry of FieldDescriptorProto * entry: DescriptorProto
    | Repeated of FieldDescriptorProto
    | Optional of FieldDescriptorProto
    | Message of FieldDescriptorProto

/// Classify each field in a message into a `FieldKind`, in `.Number` order,
/// with oneof followers absorbed into `OneofLeader.fields` (so each oneof
/// appears exactly once at the position of its lowest-numbered case).
///
/// Ordering note: an `OneofLeader` is positioned by its smallest field
/// number, NOT by its declaration index in `msg.OneofDecl`. A message that
/// declares oneof `a` (fields [5]) before oneof `b` (fields [1]) yields
/// `[OneofLeader(b, ...); OneofLeader(a, ...)]`. Callers that need
/// declaration order — for example, when emitting sibling types whose
/// source-listing order matters — should iterate `msg.OneofDecl` directly.
let classifyFields (msg: DescriptorProto) : FieldKind list =
    let mutable emittedOneofs = Set.empty

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

                let fields =
                    msg.Field
                    |> Seq.filter (fun g -> g.HasOneofIndex && not g.Proto3Optional && g.OneofIndex = idx)
                    |> Seq.sortBy (fun g -> g.Number)
                    |> Seq.toList

                Some(OneofLeader(idx, decl, fields))
        else
            match tryGetMapEntry msg f with
            | Some entry -> Some(MapEntry(f, entry))
            | None ->
                if f.Label = FieldDescriptorProto.Types.Label.Repeated then
                    Some(Repeated f)
                elif f.Proto3Optional then
                    Some(Optional f)
                elif f.Type = FieldDescriptorProto.Types.Type.Message then
                    Some(Message f)
                else
                    Some(Scalar f))
    |> Seq.toList
