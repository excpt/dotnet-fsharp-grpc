namespace Crosslang

type HelloRequest = { Name: string }

module HelloRequest =
    let empty = { Name = "" }

    let computeSize (value: HelloRequest) : int =
        let mutable size = 0

        if value.Name <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Name)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: HelloRequest) : unit =
        if value.Name <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Name)

    let encode (value: HelloRequest) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : HelloRequest =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Name = ""
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Name <- input.ReadString()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Name = _Name }

type HelloReply = { Message: string }

module HelloReply =
    let empty = { Message = "" }

    let computeSize (value: HelloReply) : int =
        let mutable size = 0

        if value.Message <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Message)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: HelloReply) : unit =
        if value.Message <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Message)

    let encode (value: HelloReply) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : HelloReply =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Message = ""
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Message <- input.ReadString()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Message = _Message }

type StreamItem = { Value: int }

module StreamItem =
    let empty = { Value = 0 }

    let computeSize (value: StreamItem) : int =
        let mutable size = 0

        if value.Value <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(value.Value)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: StreamItem) : unit =
        if value.Value <> 0 then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(value.Value)

    let encode (value: StreamItem) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : StreamItem =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Value = 0
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Value <- input.ReadInt32()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Value = _Value }

type Summary = { Count: int; Total: int }

module Summary =
    let empty = { Count = 0; Total = 0 }

    let computeSize (value: Summary) : int =
        let mutable size = 0

        if value.Count <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(value.Count)

        if value.Total <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(value.Total)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: Summary) : unit =
        if value.Count <> 0 then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(value.Count)

        if value.Total <> 0 then
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(value.Total)

    let encode (value: Summary) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : Summary =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Count = 0
        let mutable _Total = 0
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Count <- input.ReadInt32()
            | 2 -> _Total <- input.ReadInt32()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Count = _Count; Total = _Total }
