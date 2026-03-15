namespace InteropFSharp

type Status =
    | StatusUnknown = 0
    | StatusActive = 1
    | StatusInactive = 2

module Status =
    let toJsonName (value: Status) : string =
        match value with
        | Status.StatusUnknown -> "STATUS_UNKNOWN"
        | Status.StatusActive -> "STATUS_ACTIVE"
        | Status.StatusInactive -> "STATUS_INACTIVE"
        | _ -> string (int value)

    let fromJsonName (name: string) : Status =
        match name with
        | "STATUS_UNKNOWN" -> Status.StatusUnknown
        | "STATUS_ACTIVE" -> Status.StatusActive
        | "STATUS_INACTIVE" -> Status.StatusInactive
        | _ ->
            match System.Int32.TryParse(name) with
            | true, v -> LanguagePrimitives.EnumOfValue v
            | _ -> LanguagePrimitives.EnumOfValue 0

type Person = { Name: string; Age: int }

module Person =
    let empty = { Name = ""; Age = 0 }

    let computeSize (value: Person) : int =
        let mutable size = 0

        if value.Name <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Name)

        if value.Age <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(value.Age)

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: Person) : unit =
        if value.Name <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Name)

        if value.Age <> 0 then
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(value.Age)

    let encode (value: Person) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : Person =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Name = ""
        let mutable _Age = 0
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Name <- input.ReadString()
            | 2 -> _Age <- input.ReadInt32()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Name = _Name; Age = _Age }

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: Person) : unit =
        writer.WriteStartObject()

        if value.Name <> "" then
            writer.WriteString("name", value.Name)

        if value.Age <> 0 then
            writer.WriteNumber("age", value.Age)

        writer.WriteEndObject()

    let encodeJson (value: Person) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : Person =
        let mutable _Name = ""
        let mutable _Age = 0

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "name" -> _Name <- prop.Value.GetString()
            | "age" -> _Age <- prop.Value.GetInt32()
            | _ -> ()

        { Name = _Name; Age = _Age }

    let decodeJson (json: string) : Person =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

type ScalarTypes =
    { DoubleField: float
      FloatField: float32
      Int32Field: int
      Int64Field: int64
      Uint32Field: uint32
      Uint64Field: uint64
      Sint32Field: int
      Sint64Field: int64
      Fixed32Field: uint32
      Fixed64Field: uint64
      Sfixed32Field: int
      Sfixed64Field: int64
      BoolField: bool
      StringField: string
      BytesField: byte array }

module ScalarTypes =
    let empty =
        { DoubleField = 0.0
          FloatField = 0.0f
          Int32Field = 0
          Int64Field = 0L
          Uint32Field = 0u
          Uint64Field = 0UL
          Sint32Field = 0
          Sint64Field = 0L
          Fixed32Field = 0u
          Fixed64Field = 0UL
          Sfixed32Field = 0
          Sfixed64Field = 0L
          BoolField = false
          StringField = ""
          BytesField = Array.empty }

    let computeSize (value: ScalarTypes) : int =
        let mutable size = 0

        if value.DoubleField <> 0.0 then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(1) + 8

        if value.FloatField <> 0.0f then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(2) + 4

        if value.Int32Field <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(3)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(value.Int32Field)

        if value.Int64Field <> 0L then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(4)
                + Google.Protobuf.CodedOutputStream.ComputeInt64Size(value.Int64Field)

        if value.Uint32Field <> 0u then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(5)
                + Google.Protobuf.CodedOutputStream.ComputeUInt32Size(value.Uint32Field)

        if value.Uint64Field <> 0UL then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(6)
                + Google.Protobuf.CodedOutputStream.ComputeUInt64Size(value.Uint64Field)

        if value.Sint32Field <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(7)
                + Google.Protobuf.CodedOutputStream.ComputeSInt32Size(value.Sint32Field)

        if value.Sint64Field <> 0L then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(8)
                + Google.Protobuf.CodedOutputStream.ComputeSInt64Size(value.Sint64Field)

        if value.Fixed32Field <> 0u then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(9) + 4

        if value.Fixed64Field <> 0UL then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(10) + 8

        if value.Sfixed32Field <> 0 then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(11) + 4

        if value.Sfixed64Field <> 0L then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(12) + 8

        if value.BoolField then
            size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(13) + 1

        if value.StringField <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(14)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.StringField)

        if value.BytesField.Length > 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(15)
                + Google.Protobuf.CodedOutputStream.ComputeBytesSize(
                    Google.Protobuf.ByteString.CopyFrom(value.BytesField)
                )

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: ScalarTypes) : unit =
        if value.DoubleField <> 0.0 then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.Fixed64)
            output.WriteDouble(value.DoubleField)

        if value.FloatField <> 0.0f then
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.Fixed32)
            output.WriteFloat(value.FloatField)

        if value.Int32Field <> 0 then
            output.WriteTag(3, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(value.Int32Field)

        if value.Int64Field <> 0L then
            output.WriteTag(4, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt64(value.Int64Field)

        if value.Uint32Field <> 0u then
            output.WriteTag(5, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteUInt32(value.Uint32Field)

        if value.Uint64Field <> 0UL then
            output.WriteTag(6, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteUInt64(value.Uint64Field)

        if value.Sint32Field <> 0 then
            output.WriteTag(7, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteSInt32(value.Sint32Field)

        if value.Sint64Field <> 0L then
            output.WriteTag(8, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteSInt64(value.Sint64Field)

        if value.Fixed32Field <> 0u then
            output.WriteTag(9, Google.Protobuf.WireFormat.WireType.Fixed32)
            output.WriteFixed32(value.Fixed32Field)

        if value.Fixed64Field <> 0UL then
            output.WriteTag(10, Google.Protobuf.WireFormat.WireType.Fixed64)
            output.WriteFixed64(value.Fixed64Field)

        if value.Sfixed32Field <> 0 then
            output.WriteTag(11, Google.Protobuf.WireFormat.WireType.Fixed32)
            output.WriteSFixed32(value.Sfixed32Field)

        if value.Sfixed64Field <> 0L then
            output.WriteTag(12, Google.Protobuf.WireFormat.WireType.Fixed64)
            output.WriteSFixed64(value.Sfixed64Field)

        if value.BoolField then
            output.WriteTag(13, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteBool(value.BoolField)

        if value.StringField <> "" then
            output.WriteTag(14, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.StringField)

        if value.BytesField.Length > 0 then
            output.WriteTag(15, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteBytes(Google.Protobuf.ByteString.CopyFrom(value.BytesField))

    let encode (value: ScalarTypes) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : ScalarTypes =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _DoubleField = 0.0
        let mutable _FloatField = 0.0f
        let mutable _Int32Field = 0
        let mutable _Int64Field = 0L
        let mutable _Uint32Field = 0u
        let mutable _Uint64Field = 0UL
        let mutable _Sint32Field = 0
        let mutable _Sint64Field = 0L
        let mutable _Fixed32Field = 0u
        let mutable _Fixed64Field = 0UL
        let mutable _Sfixed32Field = 0
        let mutable _Sfixed64Field = 0L
        let mutable _BoolField = false
        let mutable _StringField = ""
        let mutable _BytesField = Array.empty
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _DoubleField <- input.ReadDouble()
            | 2 -> _FloatField <- input.ReadFloat()
            | 3 -> _Int32Field <- input.ReadInt32()
            | 4 -> _Int64Field <- input.ReadInt64()
            | 5 -> _Uint32Field <- input.ReadUInt32()
            | 6 -> _Uint64Field <- input.ReadUInt64()
            | 7 -> _Sint32Field <- input.ReadSInt32()
            | 8 -> _Sint64Field <- input.ReadSInt64()
            | 9 -> _Fixed32Field <- input.ReadFixed32()
            | 10 -> _Fixed64Field <- input.ReadFixed64()
            | 11 -> _Sfixed32Field <- input.ReadSFixed32()
            | 12 -> _Sfixed64Field <- input.ReadSFixed64()
            | 13 -> _BoolField <- input.ReadBool()
            | 14 -> _StringField <- input.ReadString()
            | 15 -> _BytesField <- input.ReadBytes().ToByteArray()
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { DoubleField = _DoubleField
          FloatField = _FloatField
          Int32Field = _Int32Field
          Int64Field = _Int64Field
          Uint32Field = _Uint32Field
          Uint64Field = _Uint64Field
          Sint32Field = _Sint32Field
          Sint64Field = _Sint64Field
          Fixed32Field = _Fixed32Field
          Fixed64Field = _Fixed64Field
          Sfixed32Field = _Sfixed32Field
          Sfixed64Field = _Sfixed64Field
          BoolField = _BoolField
          StringField = _StringField
          BytesField = _BytesField }

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: ScalarTypes) : unit =
        writer.WriteStartObject()

        if value.DoubleField <> 0.0 then
            if System.Double.IsNaN(value.DoubleField) then
                writer.WriteString("doubleField", "NaN")
            elif System.Double.IsPositiveInfinity(value.DoubleField) then
                writer.WriteString("doubleField", "Infinity")
            elif System.Double.IsNegativeInfinity(value.DoubleField) then
                writer.WriteString("doubleField", "-Infinity")
            else
                writer.WriteNumber("doubleField", value.DoubleField)

        if value.FloatField <> 0.0f then
            if System.Single.IsNaN(value.FloatField) then
                writer.WriteString("floatField", "NaN")
            elif System.Single.IsPositiveInfinity(value.FloatField) then
                writer.WriteString("floatField", "Infinity")
            elif System.Single.IsNegativeInfinity(value.FloatField) then
                writer.WriteString("floatField", "-Infinity")
            else
                writer.WriteNumber("floatField", float value.FloatField)

        if value.Int32Field <> 0 then
            writer.WriteNumber("int32Field", value.Int32Field)

        if value.Int64Field <> 0L then
            writer.WriteString("int64Field", string value.Int64Field)

        if value.Uint32Field <> 0u then
            writer.WriteNumber("uint32Field", value.Uint32Field)

        if value.Uint64Field <> 0UL then
            writer.WriteString("uint64Field", string value.Uint64Field)

        if value.Sint32Field <> 0 then
            writer.WriteNumber("sint32Field", value.Sint32Field)

        if value.Sint64Field <> 0L then
            writer.WriteString("sint64Field", string value.Sint64Field)

        if value.Fixed32Field <> 0u then
            writer.WriteNumber("fixed32Field", value.Fixed32Field)

        if value.Fixed64Field <> 0UL then
            writer.WriteString("fixed64Field", string value.Fixed64Field)

        if value.Sfixed32Field <> 0 then
            writer.WriteNumber("sfixed32Field", value.Sfixed32Field)

        if value.Sfixed64Field <> 0L then
            writer.WriteString("sfixed64Field", string value.Sfixed64Field)

        if value.BoolField then
            writer.WriteBoolean("boolField", value.BoolField)

        if value.StringField <> "" then
            writer.WriteString("stringField", value.StringField)

        if value.BytesField.Length > 0 then
            writer.WriteString("bytesField", System.Convert.ToBase64String(value.BytesField))

        writer.WriteEndObject()

    let encodeJson (value: ScalarTypes) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : ScalarTypes =
        let mutable _DoubleField = 0.0
        let mutable _FloatField = 0.0f
        let mutable _Int32Field = 0
        let mutable _Int64Field = 0L
        let mutable _Uint32Field = 0u
        let mutable _Uint64Field = 0UL
        let mutable _Sint32Field = 0
        let mutable _Sint64Field = 0L
        let mutable _Fixed32Field = 0u
        let mutable _Fixed64Field = 0UL
        let mutable _Sfixed32Field = 0
        let mutable _Sfixed64Field = 0L
        let mutable _BoolField = false
        let mutable _StringField = ""
        let mutable _BytesField = Array.empty

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "doubleField"
            | "double_field" ->
                _DoubleField <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        match prop.Value.GetString() with
                        | "NaN" -> System.Double.NaN
                        | "Infinity" -> System.Double.PositiveInfinity
                        | "-Infinity" -> System.Double.NegativeInfinity
                        | s -> float s
                    else
                        prop.Value.GetDouble()
            | "floatField"
            | "float_field" ->
                _FloatField <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        match prop.Value.GetString() with
                        | "NaN" -> System.Single.NaN
                        | "Infinity" -> System.Single.PositiveInfinity
                        | "-Infinity" -> System.Single.NegativeInfinity
                        | s -> float32 s
                    else
                        float32 (prop.Value.GetDouble())
            | "int32Field"
            | "int32_field" -> _Int32Field <- prop.Value.GetInt32()
            | "int64Field"
            | "int64_field" ->
                _Int64Field <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        int64 (prop.Value.GetString())
                    else
                        prop.Value.GetInt64()
            | "uint32Field"
            | "uint32_field" -> _Uint32Field <- prop.Value.GetUInt32()
            | "uint64Field"
            | "uint64_field" ->
                _Uint64Field <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        uint64 (prop.Value.GetString())
                    else
                        prop.Value.GetUInt64()
            | "sint32Field"
            | "sint32_field" -> _Sint32Field <- prop.Value.GetInt32()
            | "sint64Field"
            | "sint64_field" ->
                _Sint64Field <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        int64 (prop.Value.GetString())
                    else
                        prop.Value.GetInt64()
            | "fixed32Field"
            | "fixed32_field" -> _Fixed32Field <- prop.Value.GetUInt32()
            | "fixed64Field"
            | "fixed64_field" ->
                _Fixed64Field <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        uint64 (prop.Value.GetString())
                    else
                        prop.Value.GetUInt64()
            | "sfixed32Field"
            | "sfixed32_field" -> _Sfixed32Field <- prop.Value.GetInt32()
            | "sfixed64Field"
            | "sfixed64_field" ->
                _Sfixed64Field <-
                    if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                        int64 (prop.Value.GetString())
                    else
                        prop.Value.GetInt64()
            | "boolField"
            | "bool_field" -> _BoolField <- prop.Value.GetBoolean()
            | "stringField"
            | "string_field" -> _StringField <- prop.Value.GetString()
            | "bytesField"
            | "bytes_field" -> _BytesField <- System.Convert.FromBase64String(prop.Value.GetString())
            | _ -> ()

        { DoubleField = _DoubleField
          FloatField = _FloatField
          Int32Field = _Int32Field
          Int64Field = _Int64Field
          Uint32Field = _Uint32Field
          Uint64Field = _Uint64Field
          Sint32Field = _Sint32Field
          Sint64Field = _Sint64Field
          Fixed32Field = _Fixed32Field
          Fixed64Field = _Fixed64Field
          Sfixed32Field = _Sfixed32Field
          Sfixed64Field = _Sfixed64Field
          BoolField = _BoolField
          StringField = _StringField
          BytesField = _BytesField }

    let decodeJson (json: string) : ScalarTypes =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

type Address =
    { Street: string
      City: string
      ZipCode: string option }

module Address =
    let empty =
        { Street = ""
          City = ""
          ZipCode = None }

    let computeSize (value: Address) : int =
        let mutable size = 0

        if value.Street <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Street)

        if value.City <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.City)

        match value.ZipCode with
        | Some v ->
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(3)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(v)
        | None -> ()

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: Address) : unit =
        if value.Street <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Street)

        if value.City <> "" then
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.City)

        match value.ZipCode with
        | Some v ->
            output.WriteTag(3, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(v)
        | None -> ()

    let encode (value: Address) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : Address =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Street = ""
        let mutable _City = ""
        let mutable _ZipCode = None
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Street <- input.ReadString()
            | 2 -> _City <- input.ReadString()
            | 3 -> _ZipCode <- Some(input.ReadString())
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Street = _Street
          City = _City
          ZipCode = _ZipCode }

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: Address) : unit =
        writer.WriteStartObject()

        if value.Street <> "" then
            writer.WriteString("street", value.Street)

        if value.City <> "" then
            writer.WriteString("city", value.City)

        match value.ZipCode with
        | Some v -> writer.WriteString("zipCode", v)
        | None -> ()

        writer.WriteEndObject()

    let encodeJson (value: Address) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : Address =
        let mutable _Street = ""
        let mutable _City = ""
        let mutable _ZipCode = None

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "street" -> _Street <- prop.Value.GetString()
            | "city" -> _City <- prop.Value.GetString()
            | "zipCode"
            | "zip_code" -> _ZipCode <- Some(prop.Value.GetString())
            | _ -> ()

        { Street = _Street
          City = _City
          ZipCode = _ZipCode }

    let decodeJson (json: string) : Address =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement

type UserProfileContact =
    | PhoneNumber of phoneNumber: string
    | Email of email: string

type UserProfile =
    { Id: string
      DisplayName: string
      Status: Status
      HomeAddress: Address option
      Tags: string list
      Scores: int list
      OtherAddresses: Address list
      Metadata: Map<string, string>
      Ratings: Map<string, int>
      Rating: float option
      Contact: UserProfileContact option }

module UserProfile =
    let empty =
        { Id = ""
          DisplayName = ""
          Status = LanguagePrimitives.EnumOfValue 0
          HomeAddress = None
          Tags = []
          Scores = []
          OtherAddresses = []
          Metadata = Map.empty
          Ratings = Map.empty
          Rating = None
          Contact = None }

    let computeSize (value: UserProfile) : int =
        let mutable size = 0

        if value.Id <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.Id)

        if value.DisplayName <> "" then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(value.DisplayName)

        if int value.Status <> 0 then
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(3)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(int value.Status)

        match value.HomeAddress with
        | Some v ->
            let subSize = Address.computeSize v

            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(4)
                + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize)
                + subSize
        | None -> ()

        for item in value.Tags do
            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(5)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(item)

        if not (List.isEmpty value.Scores) then
            let mutable packedSize = 0

            for item in value.Scores do
                packedSize <- packedSize + Google.Protobuf.CodedOutputStream.ComputeInt32Size(item)

            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(6)
                + Google.Protobuf.CodedOutputStream.ComputeLengthSize(packedSize)
                + packedSize

        for item in value.OtherAddresses do
            let subSize = Address.computeSize item

            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(7)
                + Google.Protobuf.CodedOutputStream.ComputeLengthSize(subSize)
                + subSize

        for kvp in value.Metadata do
            let entrySize =
                Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(kvp.Key)
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(kvp.Value)

            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(8)
                + Google.Protobuf.CodedOutputStream.ComputeLengthSize(entrySize)
                + entrySize

        for kvp in value.Ratings do
            let entrySize =
                Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(kvp.Key)
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(kvp.Value)

            size <-
                size
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(9)
                + Google.Protobuf.CodedOutputStream.ComputeLengthSize(entrySize)
                + entrySize

        match value.Rating with
        | Some v -> size <- size + Google.Protobuf.CodedOutputStream.ComputeTagSize(10) + 8
        | None -> ()

        match value.Contact with
        | Some oneofValue ->
            match oneofValue with
            | UserProfileContact.PhoneNumber v ->
                size <-
                    size
                    + Google.Protobuf.CodedOutputStream.ComputeTagSize(11)
                    + Google.Protobuf.CodedOutputStream.ComputeStringSize(v)
            | UserProfileContact.Email v ->
                size <-
                    size
                    + Google.Protobuf.CodedOutputStream.ComputeTagSize(12)
                    + Google.Protobuf.CodedOutputStream.ComputeStringSize(v)
        | None -> ()

        size

    let writeTo (output: Google.Protobuf.CodedOutputStream) (value: UserProfile) : unit =
        if value.Id <> "" then
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.Id)

        if value.DisplayName <> "" then
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(value.DisplayName)

        if int value.Status <> 0 then
            output.WriteTag(3, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(int value.Status)

        match value.HomeAddress with
        | Some v ->
            let subSize = Address.computeSize v
            output.WriteTag(4, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteLength(subSize)
            Address.writeTo output v
        | None -> ()

        for item in value.Tags do
            output.WriteTag(5, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(item)

        if not (List.isEmpty value.Scores) then
            let mutable packedSize = 0

            for item in value.Scores do
                packedSize <- packedSize + Google.Protobuf.CodedOutputStream.ComputeInt32Size(item)

            output.WriteTag(6, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteLength(packedSize)

            for item in value.Scores do
                output.WriteInt32(item)

        for item in value.OtherAddresses do
            let subSize = Address.computeSize item
            output.WriteTag(7, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteLength(subSize)
            Address.writeTo output item

        for kvp in value.Metadata do
            let entrySize =
                Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(kvp.Key)
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(kvp.Value)

            output.WriteTag(8, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteLength(entrySize)
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(kvp.Key)
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(kvp.Value)

        for kvp in value.Ratings do
            let entrySize =
                Google.Protobuf.CodedOutputStream.ComputeTagSize(1)
                + Google.Protobuf.CodedOutputStream.ComputeStringSize(kvp.Key)
                + Google.Protobuf.CodedOutputStream.ComputeTagSize(2)
                + Google.Protobuf.CodedOutputStream.ComputeInt32Size(kvp.Value)

            output.WriteTag(9, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteLength(entrySize)
            output.WriteTag(1, Google.Protobuf.WireFormat.WireType.LengthDelimited)
            output.WriteString(kvp.Key)
            output.WriteTag(2, Google.Protobuf.WireFormat.WireType.Varint)
            output.WriteInt32(kvp.Value)

        match value.Rating with
        | Some v ->
            output.WriteTag(10, Google.Protobuf.WireFormat.WireType.Fixed64)
            output.WriteDouble(v)
        | None -> ()

        match value.Contact with
        | Some oneofValue ->
            match oneofValue with
            | UserProfileContact.PhoneNumber v ->
                output.WriteTag(11, Google.Protobuf.WireFormat.WireType.LengthDelimited)
                output.WriteString(v)
            | UserProfileContact.Email v ->
                output.WriteTag(12, Google.Protobuf.WireFormat.WireType.LengthDelimited)
                output.WriteString(v)
        | None -> ()

    let encode (value: UserProfile) : byte array =
        let size = computeSize value

        if size = 0 then
            Array.empty
        else
            let buffer = Array.zeroCreate size
            use output = new Google.Protobuf.CodedOutputStream(buffer)
            writeTo output value
            output.Flush()
            buffer

    let decode (data: byte array) : UserProfile =
        use input = new Google.Protobuf.CodedInputStream(data)
        let mutable _Id = ""
        let mutable _DisplayName = ""
        let mutable _Status = LanguagePrimitives.EnumOfValue 0
        let mutable _HomeAddress = None
        let _Tags = ResizeArray<string>()
        let _Scores = ResizeArray<int>()
        let _OtherAddresses = ResizeArray<Address>()
        let _Metadata = System.Collections.Generic.Dictionary<string, string>()
        let _Ratings = System.Collections.Generic.Dictionary<string, int>()
        let mutable _Rating = None
        let mutable _Contact = None
        let mutable tag = input.ReadTag()

        while tag <> 0u do
            match Google.Protobuf.WireFormat.GetTagFieldNumber(tag) with
            | 1 -> _Id <- input.ReadString()
            | 2 -> _DisplayName <- input.ReadString()
            | 3 -> _Status <- LanguagePrimitives.EnumOfValue(input.ReadInt32())
            | 4 ->
                let subBytes = input.ReadBytes().ToByteArray()
                _HomeAddress <- Some(Address.decode subBytes)
            | 5 -> _Tags.Add(input.ReadString())
            | 6 ->
                let wt = Google.Protobuf.WireFormat.GetTagWireType(tag)

                if wt = Google.Protobuf.WireFormat.WireType.LengthDelimited then
                    let packedData = input.ReadBytes().ToByteArray()
                    use packedInput = new Google.Protobuf.CodedInputStream(packedData)

                    while not packedInput.IsAtEnd do
                        _Scores.Add(packedInput.ReadInt32())
                else
                    _Scores.Add(input.ReadInt32())
            | 7 ->
                let subBytes = input.ReadBytes().ToByteArray()
                _OtherAddresses.Add(Address.decode subBytes)
            | 8 ->
                let entryData = input.ReadBytes().ToByteArray()
                use entryInput = new Google.Protobuf.CodedInputStream(entryData)
                let mutable key = ""
                let mutable mapValue = ""
                let mutable entryTag = entryInput.ReadTag()

                while entryTag <> 0u do
                    match Google.Protobuf.WireFormat.GetTagFieldNumber(entryTag) with
                    | 1 -> key <- entryInput.ReadString()
                    | 2 -> mapValue <- entryInput.ReadString()
                    | _ -> entryInput.SkipLastField()

                    entryTag <- entryInput.ReadTag()

                _Metadata.[key] <- mapValue
            | 9 ->
                let entryData = input.ReadBytes().ToByteArray()
                use entryInput = new Google.Protobuf.CodedInputStream(entryData)
                let mutable key = ""
                let mutable mapValue = 0
                let mutable entryTag = entryInput.ReadTag()

                while entryTag <> 0u do
                    match Google.Protobuf.WireFormat.GetTagFieldNumber(entryTag) with
                    | 1 -> key <- entryInput.ReadString()
                    | 2 -> mapValue <- entryInput.ReadInt32()
                    | _ -> entryInput.SkipLastField()

                    entryTag <- entryInput.ReadTag()

                _Ratings.[key] <- mapValue
            | 10 -> _Rating <- Some(input.ReadDouble())
            | 11 -> _Contact <- Some(UserProfileContact.PhoneNumber(input.ReadString()))
            | 12 -> _Contact <- Some(UserProfileContact.Email(input.ReadString()))
            | _ -> input.SkipLastField()

            tag <- input.ReadTag()

        { Id = _Id
          DisplayName = _DisplayName
          Status = _Status
          HomeAddress = _HomeAddress
          Tags = Seq.toList _Tags
          Scores = Seq.toList _Scores
          OtherAddresses = Seq.toList _OtherAddresses
          Metadata = _Metadata |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq
          Ratings = _Ratings |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq
          Rating = _Rating
          Contact = _Contact }

    let writeJsonTo (writer: System.Text.Json.Utf8JsonWriter) (value: UserProfile) : unit =
        writer.WriteStartObject()

        if value.Id <> "" then
            writer.WriteString("id", value.Id)

        if value.DisplayName <> "" then
            writer.WriteString("displayName", value.DisplayName)

        if int value.Status <> 0 then
            writer.WriteString("status", Status.toJsonName value.Status)

        match value.HomeAddress with
        | Some v ->
            writer.WritePropertyName("homeAddress")
            Address.writeJsonTo writer v
        | None -> ()

        if not (List.isEmpty value.Tags) then
            writer.WriteStartArray("tags")

            for item in value.Tags do
                writer.WriteStringValue(item)

            writer.WriteEndArray()

        if not (List.isEmpty value.Scores) then
            writer.WriteStartArray("scores")

            for item in value.Scores do
                writer.WriteNumberValue(item)

            writer.WriteEndArray()

        if not (List.isEmpty value.OtherAddresses) then
            writer.WriteStartArray("otherAddresses")

            for item in value.OtherAddresses do
                Address.writeJsonTo writer item

            writer.WriteEndArray()

        if not (Map.isEmpty value.Metadata) then
            writer.WriteStartObject("metadata")

            for kvp in value.Metadata do
                writer.WritePropertyName(string kvp.Key)
                writer.WriteStringValue(kvp.Value)

            writer.WriteEndObject()

        if not (Map.isEmpty value.Ratings) then
            writer.WriteStartObject("ratings")

            for kvp in value.Ratings do
                writer.WritePropertyName(string kvp.Key)
                writer.WriteNumberValue(kvp.Value)

            writer.WriteEndObject()

        match value.Rating with
        | Some v ->
            if System.Double.IsNaN(v) then
                writer.WriteString("rating", "NaN")
            elif System.Double.IsPositiveInfinity(v) then
                writer.WriteString("rating", "Infinity")
            elif System.Double.IsNegativeInfinity(v) then
                writer.WriteString("rating", "-Infinity")
            else
                writer.WriteNumber("rating", v)
        | None -> ()

        match value.Contact with
        | Some oneofValue ->
            match oneofValue with
            | UserProfileContact.PhoneNumber v -> writer.WriteString("phoneNumber", v)
            | UserProfileContact.Email v -> writer.WriteString("email", v)
        | None -> ()

        writer.WriteEndObject()

    let encodeJson (value: UserProfile) : string =
        use bufferWriter = new System.IO.MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(bufferWriter)
        writeJsonTo writer value
        writer.Flush()
        System.Text.Encoding.UTF8.GetString(bufferWriter.ToArray())

    let decodeJsonElement (element: System.Text.Json.JsonElement) : UserProfile =
        let mutable _Id = ""
        let mutable _DisplayName = ""
        let mutable _Status = LanguagePrimitives.EnumOfValue 0
        let mutable _HomeAddress = None
        let _Tags = ResizeArray<string>()
        let _Scores = ResizeArray<int>()
        let _OtherAddresses = ResizeArray<Address>()
        let _Metadata = System.Collections.Generic.Dictionary<string, string>()
        let _Ratings = System.Collections.Generic.Dictionary<string, int>()
        let mutable _Rating = None
        let mutable _Contact = None

        for prop in element.EnumerateObject() do
            match prop.Name with
            | "id" -> _Id <- prop.Value.GetString()
            | "displayName"
            | "display_name" -> _DisplayName <- prop.Value.GetString()
            | "status" ->
                _Status <-
                    Status.fromJsonName (
                        if prop.Value.ValueKind = System.Text.Json.JsonValueKind.Number then
                            string (prop.Value.GetInt32())
                        else
                            prop.Value.GetString()
                    )
            | "homeAddress"
            | "home_address" -> _HomeAddress <- Some(Address.decodeJsonElement prop.Value)
            | "tags" ->
                for item in prop.Value.EnumerateArray() do
                    _Tags.Add(item.GetString())
            | "scores" ->
                for item in prop.Value.EnumerateArray() do
                    _Scores.Add(item.GetInt32())
            | "otherAddresses"
            | "other_addresses" ->
                for item in prop.Value.EnumerateArray() do
                    _OtherAddresses.Add(Address.decodeJsonElement item)
            | "metadata" ->
                for entry in prop.Value.EnumerateObject() do
                    _Metadata.[entry.Name] <- entry.Value.GetString()
            | "ratings" ->
                for entry in prop.Value.EnumerateObject() do
                    _Ratings.[entry.Name] <- entry.Value.GetInt32()
            | "rating" ->
                _Rating <-
                    Some(
                        if prop.Value.ValueKind = System.Text.Json.JsonValueKind.String then
                            match prop.Value.GetString() with
                            | "NaN" -> System.Double.NaN
                            | "Infinity" -> System.Double.PositiveInfinity
                            | "-Infinity" -> System.Double.NegativeInfinity
                            | s -> float s
                        else
                            prop.Value.GetDouble()
                    )
            | "phoneNumber"
            | "phone_number" -> _Contact <- Some(UserProfileContact.PhoneNumber(prop.Value.GetString()))
            | "email" -> _Contact <- Some(UserProfileContact.Email(prop.Value.GetString()))
            | _ -> ()

        { Id = _Id
          DisplayName = _DisplayName
          Status = _Status
          HomeAddress = _HomeAddress
          Tags = Seq.toList _Tags
          Scores = Seq.toList _Scores
          OtherAddresses = Seq.toList _OtherAddresses
          Metadata = _Metadata |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq
          Ratings = _Ratings |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq
          Rating = _Rating
          Contact = _Contact }

    let decodeJson (json: string) : UserProfile =
        use doc = System.Text.Json.JsonDocument.Parse(json)
        decodeJsonElement doc.RootElement
