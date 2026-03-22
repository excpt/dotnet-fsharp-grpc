module internal FSharp.Grpc.Tools.ComputeSizeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.WireFormat

/// Generate computeSize function as AST widget.
let generateComputeSizeAST (msg: DescriptorProto) =
    let mutable emittedOneofs = Set.empty
    let stmts = ResizeArray<WidgetBuilder<ComputationExpressionStatement>>()

    stmts.Add(LetOrUseExpr(Value("size", ConstantExpr(Int(0))).toMutable ()))

    for f in msg.Field |> Seq.sortBy (fun f -> f.Number) do
        let tag = f.Number
        let fname = toPascalCase f.Name

        if f.HasOneofIndex && not f.Proto3Optional then
            let idx = f.OneofIndex

            if not (emittedOneofs |> Set.contains idx) then
                emittedOneofs <- emittedOneofs |> Set.add idx
                let decl = msg.OneofDecl.[idx]
                let duName = oneofTypeName msg.Name decl.Name
                let duFieldName = toPascalCase decl.Name

                let oneofCases =
                    msg.Field
                    |> Seq.filter (fun of' -> of'.HasOneofIndex && not of'.Proto3Optional && of'.OneofIndex = idx)
                    |> Seq.sortBy (fun of' -> of'.Number)
                    |> Seq.toList

                let clauses =
                    oneofCases
                    |> List.map (fun c ->
                        let caseName = toPascalCase c.Name
                        let caseTag = c.Number

                        if c.Type = FieldDescriptorProto.Types.Type.Message then
                            let msgTypeName = simpleTypeName c.TypeName

                            MatchClauseExpr(
                                ConstantPat(Constant($"{duName}.{caseName} v")),
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(Value("subSize", E $"{msgTypeName}.computeSize v"))
                                      OtherExpr(
                                          addToSize (
                                              $"Google.Protobuf.CodedOutputStream.ComputeTagSize({caseTag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                          )
                                      ) ]
                                )
                            )
                        else
                            let cse = computeSizeExpr "v" c

                            MatchClauseExpr(
                                ConstantPat(Constant($"{duName}.{caseName} v")),
                                addToSize ($"Google.Protobuf.CodedOutputStream.ComputeTagSize({caseTag}) + {cse}")
                            ))

                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{duFieldName}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some oneofValue")),
                                  MatchExpr(E "oneofValue", clauses)
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
        elif tryGetMapEntry msg f |> Option.isSome then
            let entry = (tryGetMapEntry msg f).Value
            let keyField = entry.Field.[0]
            let valueField = entry.Field.[1]
            let keySizeExpr = computeSizeExpr "mapKey" keyField

            if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName valueField.TypeName

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> Map.iter (fun mapKey mapValue -> let valueMsgSize = {msgTypeName}.computeSize mapValue in let entrySize = Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(valueMsgSize) + valueMsgSize in size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(entrySize) + entrySize)"
                    )
                )
            else
                let valueSizeExpr =
                    match valueField.Type with
                    | FieldDescriptorProto.Types.Type.Enum -> computeSizeExpr "int mapValue" valueField
                    | _ -> computeSizeExpr "mapValue" valueField

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> Map.iter (fun mapKey mapValue -> let entrySize = Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + {valueSizeExpr} in size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(entrySize) + entrySize)"
                    )
                )
        elif f.Label = FieldDescriptorProto.Types.Label.Repeated then
            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName f.TypeName

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> let subSize = {msgTypeName}.computeSize item in size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize)"
                    )
                )
            elif f.Type = FieldDescriptorProto.Types.Type.String then
                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeStringSize(item))"
                    )
                )
            elif f.Type = FieldDescriptorProto.Types.Type.Bytes then
                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeBytesSize(Google.Protobuf.ByteString.CopyFrom(item)))"
                    )
                )
            else
                let itemSizeExpr =
                    match f.Type with
                    | FieldDescriptorProto.Types.Type.Enum -> computeSizeExpr "int item" f
                    | _ -> computeSizeExpr "item" f

                stmts.Add(
                    OtherExpr(
                        IfThenExpr(
                            E $"not (List.isEmpty value.{fname})",
                            CompExprBodyExpr(
                                [ LetOrUseExpr(Value("packedSize", ConstantExpr(Int(0))).toMutable ())
                                  OtherExpr(
                                      E
                                          $"value.{fname} |> List.iter (fun item -> packedSize <- packedSize + {itemSizeExpr})"
                                  )
                                  OtherExpr(
                                      addToSize (
                                          $"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(packedSize) + packedSize"
                                      )
                                  ) ]
                            )
                        )
                    )
                )
        elif f.Proto3Optional then
            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName f.TypeName

                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  CompExprBodyExpr(
                                      [ LetOrUseExpr(Value("subSize", E $"{msgTypeName}.computeSize v"))
                                        OtherExpr(
                                            addToSize (
                                                $"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                            )
                                        ) ]
                                  )
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
            else
                let cse = computeSizeExpr "v" f

                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  addToSize ($"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + {cse}")
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
        elif f.Type = FieldDescriptorProto.Types.Type.Message then
            let msgTypeName = simpleTypeName f.TypeName

            match Map.tryFind f.TypeName wellKnownTypeMap with
            | Some _ ->
                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  CompExprBodyExpr(
                                      [ LetOrUseExpr(Value("subSize", E "v.CalculateSize()"))
                                        OtherExpr(
                                            addToSize (
                                                $"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                            )
                                        ) ]
                                  )
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
            | None ->
                match Map.tryFind f.TypeName wrapperTypeMap with
                | Some _ ->
                    let wrapperClassName = simpleTypeName f.TypeName

                    stmts.Add(
                        OtherExpr(
                            MatchExpr(
                                E $"value.{fname}",
                                [ MatchClauseExpr(
                                      ConstantPat(Constant("Some v")),
                                      CompExprBodyExpr(
                                          [ LetOrUseExpr(
                                                Value(
                                                    "wrapper",
                                                    E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()"
                                                )
                                            )
                                            OtherExpr(E "wrapper.Value <- v")
                                            LetOrUseExpr(Value("subSize", E "wrapper.CalculateSize()"))
                                            OtherExpr(
                                                addToSize (
                                                    $"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                                )
                                            ) ]
                                      )
                                  )
                                  MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                            )
                        )
                    )
                | None ->
                    stmts.Add(
                        OtherExpr(
                            MatchExpr(
                                E $"value.{fname}",
                                [ MatchClauseExpr(
                                      ConstantPat(Constant("Some v")),
                                      CompExprBodyExpr(
                                          [ LetOrUseExpr(Value("subSize", E $"{msgTypeName}.computeSize v"))
                                            OtherExpr(
                                                addToSize (
                                                    $"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                                )
                                            ) ]
                                      )
                                  )
                                  MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                            )
                        )
                    )
        else
            let check = defaultCheckExpr fname f
            let ve = computeSizeValueExpr fname f
            let cse = computeSizeExpr ve f

            stmts.Add(
                OtherExpr(
                    IfThenExpr(E check, addToSize ($"Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + {cse}"))
                )
            )

    stmts.Add(OtherExpr(E "size"))

    Function(
        "computeSize",
        ParenPat(ParameterPat("value", LongIdent(msg.Name))),
        CompExprBodyExpr(stmts),
        LongIdent("int")
    )
