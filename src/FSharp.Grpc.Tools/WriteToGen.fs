module internal FSharp.Grpc.Tools.WriteToGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.FieldClass
open FSharp.Grpc.Tools.WireFormat

/// Generate writeTo function as AST widget.
let generateWriteToAST (currentMsgFqn: string) (msg: DescriptorProto) =
    let stmts = ResizeArray<WidgetBuilder<ComputationExpressionStatement>>()
    let classified = classifyFields msg

    for kind in classified do
        match kind with
        | OneofLeader(_, decl, oneofCases) ->
            let duName = oneofTypeName msg.Name decl.Name
            let duFieldName = toPascalCase decl.Name

            let clauses =
                oneofCases
                |> List.map (fun c ->
                    let caseName = toPascalCase c.Name
                    let caseTag = c.Number
                    let caseWt = wireType c

                    if c.Type = FieldDescriptorProto.Types.Type.Message then
                        let body =
                            match classifyMessageRef currentMsgFqn c.TypeName with
                            | WellKnown _ ->
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(Value("subSize", E "v.CalculateSize()"))
                                      OtherExpr(
                                          E
                                              $"output.WriteTag({caseTag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                      )
                                      OtherExpr(E "output.WriteLength(subSize)")
                                      OtherExpr(E "v.WriteTo(output)") ]
                                )
                            | Wrapper(_, wrapperClassName) ->
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(
                                          Value("wrapper", E $"Google.Protobuf.WellKnownTypes.{wrapperClassName}()")
                                      )
                                      OtherExpr(E "wrapper.Value <- v")
                                      LetOrUseExpr(Value("subSize", E "wrapper.CalculateSize()"))
                                      OtherExpr(
                                          E
                                              $"output.WriteTag({caseTag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                      )
                                      OtherExpr(E "output.WriteLength(subSize)")
                                      OtherExpr(E "wrapper.WriteTo(output)") ]
                                )
                            | PlainMessage msgTypeName ->
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(Value("subSize", E $"{msgTypeName}.computeSize v"))
                                      OtherExpr(
                                          E
                                              $"output.WriteTag({caseTag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                      )
                                      OtherExpr(E "output.WriteLength(subSize)")
                                      OtherExpr(E $"{msgTypeName}.writeTo output v") ]
                                )

                        MatchClauseExpr(ConstantPat(Constant($"{duName}.{caseName} v")), body)
                    else
                        let writeCall = writeExpr "output" "v" c

                        MatchClauseExpr(
                            ConstantPat(Constant($"{duName}.{caseName} v")),
                            CompExprBodyExpr(
                                [ OtherExpr(
                                      E $"output.WriteTag({caseTag}, Google.Protobuf.WireFormat.WireType.{caseWt})"
                                  )
                                  OtherExpr(E writeCall) ]
                            )
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
            let keyWt = wireType keyField
            let keyWriteCall = writeExpr "output" "mapKey" keyField
            let keySizeExpr = computeSizeExpr "mapKey" keyField

            if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = qualifiedRef currentMsgFqn valueField.TypeName

                stmts.Add(
                    OtherExpr(
                        ForEachDoExpr(
                            "kvp",
                            E $"value.{fname}",
                            CompExprBodyExpr(
                                [ LetOrUseExpr(Value("mapKey", E "kvp.Key"))
                                  LetOrUseExpr(Value("mapValue", E "kvp.Value"))
                                  LetOrUseExpr(Value("valueMsgSize", E $"{msgTypeName}.computeSize mapValue"))
                                  LetOrUseExpr(
                                      Value(
                                          "entrySize",
                                          E
                                              $"Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(valueMsgSize) + valueMsgSize"
                                      )
                                  )
                                  OtherExpr(
                                      E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                  )
                                  OtherExpr(E "output.WriteLength(entrySize)")
                                  OtherExpr(E $"output.WriteTag(1, Google.Protobuf.WireFormat.WireType.{keyWt})")
                                  OtherExpr(E keyWriteCall)
                                  OtherExpr(E "output.WriteTag(2, Google.Protobuf.WireFormat.WireType.LengthDelimited)")
                                  OtherExpr(E "output.WriteLength(valueMsgSize)")
                                  OtherExpr(E $"{msgTypeName}.writeTo output mapValue") ]
                            )
                        )
                    )
                )
            else
                let valueWt = wireType valueField
                let valueWriteCall = writeExpr "output" "mapValue" valueField
                let valueSizeExpr = computeSizeExpr "mapValue" valueField

                stmts.Add(
                    OtherExpr(
                        ForEachDoExpr(
                            "kvp",
                            E $"value.{fname}",
                            CompExprBodyExpr(
                                [ LetOrUseExpr(Value("mapKey", E "kvp.Key"))
                                  LetOrUseExpr(Value("mapValue", E "kvp.Value"))
                                  LetOrUseExpr(
                                      Value(
                                          "entrySize",
                                          E
                                              $"Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + {valueSizeExpr}"
                                      )
                                  )
                                  OtherExpr(
                                      E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                  )
                                  OtherExpr(E "output.WriteLength(entrySize)")
                                  OtherExpr(E $"output.WriteTag(1, Google.Protobuf.WireFormat.WireType.{keyWt})")
                                  OtherExpr(E keyWriteCall)
                                  OtherExpr(E $"output.WriteTag(2, Google.Protobuf.WireFormat.WireType.{valueWt})")
                                  OtherExpr(E valueWriteCall) ]
                            )
                        )
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
                            $"value.{fname} |> List.iter (fun item -> let subSize = {msgTypeName}.computeSize item in output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteLength(subSize); {msgTypeName}.writeTo output item)"
                    )
                )
            elif
                f.Type = FieldDescriptorProto.Types.Type.String
                || f.Type = FieldDescriptorProto.Types.Type.Bytes
            then
                // Length-delimited per-item — not packed.
                let writeItem = writeExpr "output" "item" f

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); {writeItem})"
                    )
                )
            else
                let writeItem = writeExpr "output" "item" f
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
                                      E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                  )
                                  OtherExpr(E $"output.WriteLength(packedSize)")
                                  OtherExpr(E $"value.{fname} |> List.iter (fun item -> {writeItem})") ]
                            )
                        )
                    )
                )
        | Optional f ->
            let tag = f.Number
            let fname = toPascalCase f.Name
            let wt = wireType f

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
                                            E
                                                $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                        )
                                        OtherExpr(E $"output.WriteLength(subSize)")
                                        OtherExpr(E $"{msgTypeName}.writeTo output v") ]
                                  )
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
            else
                let writeCall = writeExpr "output" "v" f

                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  CompExprBodyExpr(
                                      [ OtherExpr(E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.{wt})")
                                        OtherExpr(E writeCall) ]
                                  )
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
                                            E
                                                $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                        )
                                        OtherExpr(E $"output.WriteLength(subSize)")
                                        OtherExpr(E "v.WriteTo(output)") ]
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
                                            E
                                                $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                        )
                                        OtherExpr(E $"output.WriteLength(subSize)")
                                        OtherExpr(E "wrapper.WriteTo(output)") ]
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
                                            E
                                                $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                        )
                                        OtherExpr(E $"output.WriteLength(subSize)")
                                        OtherExpr(E $"{msgTypeName}.writeTo output v") ]
                                  )
                              )
                              MatchClauseExpr(ConstantPat(Constant("None")), E "()") ]
                        )
                    )
                )
        | Scalar f ->
            let tag = f.Number
            let fname = toPascalCase f.Name
            let wt = wireType f
            let check = defaultCheckExpr fname f
            let writeCall = writeExpr "output" $"value.{fname}" f

            stmts.Add(
                OtherExpr(
                    IfThenExpr(
                        E check,
                        CompExprBodyExpr(
                            [ OtherExpr(E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.{wt})")
                              OtherExpr(E writeCall) ]
                        )
                    )
                )
            )

    if List.isEmpty classified then
        stmts.Add(OtherExpr(E "()"))

    Function(
        "writeTo",
        [ ParenPat(ParameterPat("output", LongIdent("Google.Protobuf.CodedOutputStream")))
          ParenPat(ParameterPat("value", LongIdent(msg.Name))) ],
        CompExprBodyExpr(stmts),
        LongIdent("unit")
    )

/// Generate encode function as AST widget.
let generateEncodeAST (msg: DescriptorProto) =
    Function(
        "encode",
        ParenPat(ParameterPat("value", LongIdent(msg.Name))),
        CompExprBodyExpr(
            [ LetOrUseExpr(Value("size", E "computeSize value"))
              OtherExpr(
                  IfThenElseExpr(
                      E "size = 0",
                      E "Array.empty",
                      CompExprBodyExpr(
                          [ LetOrUseExpr(Value("buffer", E "Array.zeroCreate size"))
                            LetOrUseExpr(Use("output", E "new Google.Protobuf.CodedOutputStream(buffer)"))
                            OtherExpr(E "writeTo output value")
                            OtherExpr(E "output.Flush()")
                            OtherExpr(E "buffer") ]
                      )
                  )
              ) ]
        ),
        LongIdent("byte array")
    )
