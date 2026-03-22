namespace Testservice

type PingRequest = { Message: string }

module PingRequest =
    let empty = { Message = "" }

    let computeSize (value: PingRequest) : int =
        let mutable size = 0

        if value.Message <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Message)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: PingRequest) : unit =
        if value.Message <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Message)

    let encode (value: PingRequest) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decodeFrom (input: Google.Protobuf.CodedInputStream) : PingRequest =
        let mutable _Message = ""
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Message <- input.ReadString()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Message = _Message }

    let decode (data: byte array) : PingRequest =
        use input = new Google.Protobuf.CodedInputStream(data)
        decodeFrom input

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: PingRequest) : unit =
        writer.WriteStartObject()

        if value.Message <> "" then
            writer.WriteString("message", value.Message)

        writer.WriteEndObject()

    let encodeJson (value: PingRequest) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : PingRequest =
        let mutable _Message = ""

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "message" -> _Message <- prop.Value.GetString()
            | _ -> ()

        { Message = _Message }

    let decodeJson (json: string) : PingRequest =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

type PingReply = { Message: string }

module PingReply =
    let empty = { Message = "" }

    let computeSize (value: PingReply) : int =
        let mutable size = 0

        if value.Message <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Message)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: PingReply) : unit =
        if value.Message <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Message)

    let encode (value: PingReply) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decodeFrom (input: Google.Protobuf.CodedInputStream) : PingReply =
        let mutable _Message = ""
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Message <- input.ReadString()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Message = _Message }

    let decode (data: byte array) : PingReply =
        use input = new Google.Protobuf.CodedInputStream(data)
        decodeFrom input

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: PingReply) : unit =
        writer.WriteStartObject()

        if value.Message <> "" then
            writer.WriteString("message", value.Message)

        writer.WriteEndObject()

    let encodeJson (value: PingReply) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : PingReply =
        let mutable _Message = ""

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "message" -> _Message <- prop.Value.GetString()
            | _ -> ()

        { Message = _Message }

    let decodeJson (json: string) : PingReply =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

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

    let decodeFrom (input: Google.Protobuf.CodedInputStream) : StreamItem =
        let mutable _Value = 0
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Value <- input.ReadInt32()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Value = _Value }

    let decode (data: byte array) : StreamItem =
        use input = new Google.Protobuf.CodedInputStream(data)
        decodeFrom input

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: StreamItem) : unit =
        writer.WriteStartObject()

        if value.Value <> 0 then
            writer.WriteNumber("value", value.Value)

        writer.WriteEndObject()

    let encodeJson (value: StreamItem) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : StreamItem =
        let mutable _Value = 0

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "value" -> _Value <- prop.Value.GetInt32()
            | _ -> ()

        { Value = _Value }

    let decodeJson (json: string) : StreamItem =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

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

    let decodeFrom (input: Google.Protobuf.CodedInputStream) : Summary =
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

    let decode (data: byte array) : Summary =
        use input = new Google.Protobuf.CodedInputStream(data)
        decodeFrom input

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: Summary) : unit =
        writer.WriteStartObject()

        if value.Count <> 0 then
            writer.WriteNumber("count", value.Count)

        if value.Total <> 0 then
            writer.WriteNumber("total", value.Total)

        writer.WriteEndObject()

    let encodeJson (value: Summary) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : Summary =
        let mutable _Count = 0
        let mutable _Total = 0

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "count" -> _Count <- prop.Value.GetInt32()
            | "total" -> _Total <- prop.Value.GetInt32()
            | _ -> ()

        { Count = _Count; Total = _Total }

    let decodeJson (json: string) : Summary =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement
