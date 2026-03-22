module internal FSharp.Grpc.Tools.WriteToGen

open Google.Protobuf.Reflection
open Fabulous.AST
open Fantomas.Core.SyntaxOak
open type Fabulous.AST.Ast
open FSharp.Grpc.Tools.TypeMapping
open FSharp.Grpc.Tools.WireFormat

/// Generate writeTo function as AST widget.
let generateWriteToAST (msg: DescriptorProto) =
    let mutable emittedOneofs = Set.empty
    let stmts = ResizeArray<WidgetBuilder<ComputationExpressionStatement>>()
    let mutable hasBody = false

    for f in msg.Field |> Seq.sortBy (fun f -> f.Number) do
        hasBody <- true
        let tag = f.Number
        let fname = toPascalCase f.Name
        let wt = wireType f

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
                        let caseWt = wireType c

                        if c.Type = FieldDescriptorProto.Types.Type.Message then
                            let msgTypeName = simpleTypeName c.TypeName

                            MatchClauseExpr(
                                ConstantPat(Constant($"{duName}.{caseName} v")),
                                CompExprBodyExpr(
                                    [ LetOrUseExpr(Value("subSize", E $"{msgTypeName}.computeSize v"))
                                      OtherExpr(
                                          E
                                              $"output.WriteTag({caseTag}, Google.Protobuf.WireFormat.WireType.LengthDelimited)"
                                      )
                                      OtherExpr(E $"output.WriteLength(subSize)")
                                      OtherExpr(E $"{msgTypeName}.writeTo output v") ]
                                )
                            )
                        else
                            let caseWrite = writeMethod c

                            let castPrefix =
                                if c.Type = FieldDescriptorProto.Types.Type.Enum then
                                    "int "
                                else
                                    ""

                            MatchClauseExpr(
                                ConstantPat(Constant($"{duName}.{caseName} v")),
                                CompExprBodyExpr(
                                    [ OtherExpr(
                                          E $"output.WriteTag({caseTag}, Google.Protobuf.WireFormat.WireType.{caseWt})"
                                      )
                                      OtherExpr(E $"output.{caseWrite}({castPrefix}v)") ]
                                )
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
            let keyWt = wireType keyField
            let keyWrite = writeMethod keyField
            let keySizeExpr = computeSizeExpr "mapKey" keyField

            if valueField.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName valueField.TypeName

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> Map.iter (fun mapKey mapValue -> let valueMsgSize = {msgTypeName}.computeSize mapValue in let entrySize = Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + Google.Protobuf.CodedOutputStream.ComputeLengthSize(valueMsgSize) + valueMsgSize in output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteLength(entrySize); output.WriteTag(1, Google.Protobuf.WireFormat.WireType.{keyWt}); output.{keyWrite}(mapKey); output.WriteTag(2, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteLength(valueMsgSize); {msgTypeName}.writeTo output mapValue)"
                    )
                )
            else
                let valueWt = wireType valueField
                let valueWrite = writeMethod valueField

                let valueSizeExpr =
                    match valueField.Type with
                    | FieldDescriptorProto.Types.Type.Enum -> computeSizeExpr "int mapValue" valueField
                    | _ -> computeSizeExpr "mapValue" valueField

                let castPrefix =
                    if valueField.Type = FieldDescriptorProto.Types.Type.Enum then
                        "int "
                    else
                        ""

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> Map.iter (fun mapKey mapValue -> let entrySize = Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + {keySizeExpr} + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + {valueSizeExpr} in output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteLength(entrySize); output.WriteTag(1, Google.Protobuf.WireFormat.WireType.{keyWt}); output.{keyWrite}(mapKey); output.WriteTag(2, Google.Protobuf.WireFormat.WireType.{valueWt}); output.{valueWrite}({castPrefix}mapValue))"
                    )
                )
        elif f.Label = FieldDescriptorProto.Types.Label.Repeated then
            if f.Type = FieldDescriptorProto.Types.Type.Message then
                let msgTypeName = simpleTypeName f.TypeName

                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> let subSize = {msgTypeName}.computeSize item in output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteLength(subSize); {msgTypeName}.writeTo output item)"
                    )
                )
            elif f.Type = FieldDescriptorProto.Types.Type.String then
                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteString(item))"
                    )
                )
            elif f.Type = FieldDescriptorProto.Types.Type.Bytes then
                stmts.Add(
                    OtherExpr(
                        E
                            $"value.{fname} |> List.iter (fun item -> output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.LengthDelimited); output.WriteBytes(Google.Protobuf.ByteString.CopyFrom(item)))"
                    )
                )
            else
                let wm = writeMethod f

                let itemSizeExpr =
                    match f.Type with
                    | FieldDescriptorProto.Types.Type.Enum -> computeSizeExpr "int item" f
                    | _ -> computeSizeExpr "item" f

                let castPrefix =
                    if f.Type = FieldDescriptorProto.Types.Type.Enum then
                        "int "
                    else
                        ""

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
                                  OtherExpr(E $"value.{fname} |> List.iter (fun item -> output.{wm}({castPrefix}item))") ]
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
                let wm = writeMethod f

                let castPrefix =
                    if f.Type = FieldDescriptorProto.Types.Type.Enum then
                        "int "
                    else
                        ""

                stmts.Add(
                    OtherExpr(
                        MatchExpr(
                            E $"value.{fname}",
                            [ MatchClauseExpr(
                                  ConstantPat(Constant("Some v")),
                                  CompExprBodyExpr(
                                      [ OtherExpr(E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.{wt})")
                                        OtherExpr(E $"output.{wm}({castPrefix}v)") ]
                                  )
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
            let check = defaultCheckExpr fname f
            let wm = writeMethod f
            let ve = writeValueExpr fname f

            stmts.Add(
                OtherExpr(
                    IfThenExpr(
                        E check,
                        CompExprBodyExpr(
                            [ OtherExpr(E $"output.WriteTag({tag}, Google.Protobuf.WireFormat.WireType.{wt})")
                              OtherExpr(E $"output.{wm}({ve})") ]
                        )
                    )
                )
            )

    if not hasBody then
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
