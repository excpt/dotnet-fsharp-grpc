namespace Greeter

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

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: HelloRequest) : unit =
        writer.WriteStartObject()

        if value.Name <> "" then
            writer.WriteString("name", value.Name)

        writer.WriteEndObject()

    let encodeJson (value: HelloRequest) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : HelloRequest =
        let mutable _Name = ""

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "name" -> _Name <- prop.Value.GetString()
            | _ -> ()

        { Name = _Name }

    let decodeJson (json: string) : HelloRequest =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

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

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: HelloReply) : unit =
        writer.WriteStartObject()

        if value.Message <> "" then
            writer.WriteString("message", value.Message)

        writer.WriteEndObject()

    let encodeJson (value: HelloReply) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : HelloReply =
        let mutable _Message = ""

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "message" -> _Message <- prop.Value.GetString()
            | _ -> ()

        { Message = _Message }

    let decodeJson (json: string) : HelloReply =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement
