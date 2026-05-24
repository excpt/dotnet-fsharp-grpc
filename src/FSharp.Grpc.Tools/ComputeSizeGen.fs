module internal FSharp.Grpc.Tools.ComputeSizeGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.FieldClass
open FSharp.Grpc.Tools.WireFormat

/// Generate computeSize function as AST widget.
let generateComputeSizeAST (currentMsgFqn: string) (msg: DescriptorProto) =
    let stmts = ResizeArray<WidgetBuilder<ComputationExpressionStatement>>()

    stmts.Add(LetOrUseExpr(Value("size", ConstantExpr(Int(0))).toMutable ()))

    for kind in classifyFields msg do
        match kind with
        | OneofLeader(_, decl, oneofCases) ->
            let duName = oneofTypeName msg.Name decl.Name
            let duFieldName = toPascalCase decl.Name

            let clauses =
                oneofCases
                |> List.map (fun c ->
                    let caseName = toPascalCase c.Name
                    let caseTag = c.Number

                    if c.Type = FieldDescriptorProto.Types.Type.Message then
                        let body =
                            match classifyMessageRef currentMsgFqn c.TypeName with
                            | WellKnown _ ->
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(Value("subSize", E "v.CalculateSize()"))
                                      OtherExpr(
                                          addToSize (
                                              $"Google.Protobuf.CodedOutputStream.ComputeTagSize({caseTag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                          )
                                      ) ]
                                )
                            | Wrapper(_, wrapperClassName) ->
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(
                                          Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()")
                                      )
                                      OtherExpr(E "wrapper.Value <- v")
                                      LetOrUseExpr(Value("subSize", E "wrapper.CalculateSize()"))
                                      OtherExpr(
                                          addToSize (
                                              $"Google.Protobuf.CodedOutputStream.ComputeTagSize({caseTag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                          )
                                      ) ]
                                )
                            | PlainMessage msgTypeName ->
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(Value("subSize", E $"{msgTypeName}.computeSize v"))
                                      OtherExpr(
                                          addToSize (
                                              $"Google.Protobuf.CodedOutputStream.ComputeTagSize({caseTag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize"
                                          )
                                      ) ]
                                )

                        MatchClauseExpr(ConstantPat(Constant($"{duName}.{caseName} v")), body)
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
                        [ MatchClauseExpr(ConstantPat(Constant("Some oneofValue")), MatchExpr(E "oneofValue", clauses))
                          MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                    )
                )
            )
        | MapEntry(f, entry) ->
            let tag = f.Number
            let fname = toPascalCase f.Name
            let keyField = entry.Field.[0]
            let valueField = entry.Field.[1]
            let keySizeExpr = computeSizeExpr "mapKey" keyField

            if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = qualifiedRef currentMsgFqn valueField.TypeName

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> Map.iter (fun mapKey mapValue -> let valueMsgSize = {msgTypeName}.computeSize mapValue in let entrySize = Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(valueMsgSize) + valueMsgSize in size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(entrySize) + entrySize)"
                    )
                )
            else
                let valueSizeExpr = computeSizeExpr "mapValue" valueField

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> Map.iter (fun mapKey mapValue -> let entrySize = Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + {valueSizeExpr} in size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(entrySize) + entrySize)"
                    )
                )
        | Repeated f ->
            let tag = f.Number
            let fname = toPascalCase f.Name

            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> let subSize = {msgTypeName}.computeSize item in size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize) + subSize)"
                    )
                )
            elif
                f.Type = FieldDescriptorProto.Types.Type.String
                || f.Type = FieldDescriptorProto.Types.Type.Bytes
            then
                // Length-delimited per-item — not packed.
                let itemSizeExpr = computeSizeExpr "item" f

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize({tag}) + {itemSizeExpr})"
                    )
                )
            else
                let itemSizeExpr = computeSizeExpr "item" f

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
        | Optional f ->
            let tag = f.Number
            let fname = toPascalCase f.Name

            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = qualifiedRef currentMsgFqn f.TypeName

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
        | Message f ->
            let tag = f.Number
            let fname = toPascalCase f.Name

            match classifyMessageRef currentMsgFqn f.TypeName with
            | WellKnown _ ->
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
            | Wrapper(_, wrapperClassName) ->
                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  CompExprBodyExpr(
                                      [ LetOrUseExpr(
                                            Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()")
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
            | PlainMessage msgTypeName ->
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
        | Scalar f ->
            let tag = f.Number
            let fname = toPascalCase f.Name
            let check = defaultCheckExpr fname f
            let cse = computeSizeExpr $"value.{fname}" f

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
