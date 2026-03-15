using BenchmarkDotNet.Attributes;

using Google.Protobuf;

using Cs = Interop;
using Fs = InteropFSharp;

[MemoryDiagnoser]
public class PersonJsonBenchmarks
{
    private readonly Cs.Person _csPerson = new() { Name = "Alice Johnson", Age = 42 };
    private readonly Fs.Person _fsPerson = new("Alice Johnson", 42);
    private readonly JsonFormatter _formatter = JsonFormatter.Default;
    private readonly JsonParser _parser = JsonParser.Default;
    private string _json = null!;

    [GlobalSetup]
    public void Setup()
    {
        _json = _formatter.Format(_csPerson);
    }

    [Benchmark(Description = "C# JSON Encode")]
    public string CSharp_JsonEncode() => _formatter.Format(_csPerson);

    [Benchmark(Description = "F# JSON Encode")]
    public string FSharp_JsonEncode() => Fs.PersonModule.encodeJson(_fsPerson);

    [Benchmark(Description = "C# JSON Decode")]
    public Cs.Person CSharp_JsonDecode() => _parser.Parse<Cs.Person>(_json);

    [Benchmark(Description = "F# JSON Decode")]
    public Fs.Person FSharp_JsonDecode() => Fs.PersonModule.decodeJson(_json);
}

[MemoryDiagnoser]
public class ScalarTypesJsonBenchmarks
{
    private readonly Cs.ScalarTypes _csScalars = new()
    {
        DoubleField = 3.14159265358979,
        FloatField = 2.71828f,
        Int32Field = 42,
        Int64Field = 1234567890123L,
        Uint32Field = 4294967295u,
        Uint64Field = 18446744073709551615UL,
        Sint32Field = -42,
        Sint64Field = -1234567890123L,
        Fixed32Field = 12345u,
        Fixed64Field = 123456789UL,
        Sfixed32Field = -12345,
        Sfixed64Field = -123456789L,
        BoolField = true,
        StringField = "hello protobuf benchmarks",
        BytesField = ByteString.CopyFrom(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF })
    };

    private readonly Fs.ScalarTypes _fsScalars = new(
        doubleField: 3.14159265358979,
        floatField: 2.71828f,
        int32Field: 42,
        int64Field: 1234567890123L,
        uint32Field: 4294967295u,
        uint64Field: 18446744073709551615UL,
        sint32Field: -42,
        sint64Field: -1234567890123L,
        fixed32Field: 12345u,
        fixed64Field: 123456789UL,
        sfixed32Field: -12345,
        sfixed64Field: -123456789L,
        boolField: true,
        stringField: "hello protobuf benchmarks",
        bytesField: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
    );

    private readonly JsonFormatter _formatter = JsonFormatter.Default;
    private readonly JsonParser _parser = JsonParser.Default;
    private string _json = null!;

    [GlobalSetup]
    public void Setup()
    {
        _json = _formatter.Format(_csScalars);
    }

    [Benchmark(Description = "C# JSON Encode")]
    public string CSharp_JsonEncode() => _formatter.Format(_csScalars);

    [Benchmark(Description = "F# JSON Encode")]
    public string FSharp_JsonEncode() => Fs.ScalarTypesModule.encodeJson(_fsScalars);

    [Benchmark(Description = "C# JSON Decode")]
    public Cs.ScalarTypes CSharp_JsonDecode() => _parser.Parse<Cs.ScalarTypes>(_json);

    [Benchmark(Description = "F# JSON Decode")]
    public Fs.ScalarTypes FSharp_JsonDecode() => Fs.ScalarTypesModule.decodeJson(_json);
}

[MemoryDiagnoser]
public class UserProfileJsonBenchmarks
{
    private readonly Cs.UserProfile _csProfile;
    private readonly Fs.UserProfile _fsProfile;
    private readonly JsonFormatter _formatter = JsonFormatter.Default;
    private readonly JsonParser _parser = JsonParser.Default;
    private string _json = null!;

    public UserProfileJsonBenchmarks()
    {
        var csAddr = new Cs.Address { Street = "123 Main St", City = "Springfield", ZipCode = "62704" };

        _csProfile = new Cs.UserProfile
        {
            Id = "user-12345",
            DisplayName = "Alice Johnson",
            Status = Cs.Status.Active,
            HomeAddress = csAddr,
            Tags = { "admin", "developer", "reviewer" },
            Scores = { 100, 95, 87, 92, 88 },
            OtherAddresses =
            {
                new Cs.Address { Street = "456 Oak Ave", City = "Portland" },
                new Cs.Address { Street = "789 Pine Rd", City = "Seattle", ZipCode = "98101" }
            },
            Metadata =
            {
                { "team", "platform" },
                { "level", "senior" },
                { "office", "remote" }
            },
            Ratings =
            {
                { "code_quality", 9 },
                { "communication", 8 },
                { "reliability", 10 }
            },
            Rating = 4.7,
            Email = "alice@example.com"
        };

        var fsAddr = new Fs.Address("123 Main St", "Springfield", Microsoft.FSharp.Core.FSharpOption<string>.Some("62704"));

        _fsProfile = new Fs.UserProfile(
            id: "user-12345",
            displayName: "Alice Johnson",
            status: Fs.Status.StatusActive,
            homeAddress: Microsoft.FSharp.Core.FSharpOption<Fs.Address>.Some(fsAddr),
            tags: Microsoft.FSharp.Collections.ListModule.OfArray(new[] { "admin", "developer", "reviewer" }),
            scores: Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 100, 95, 87, 92, 88 }),
            otherAddresses: Microsoft.FSharp.Collections.ListModule.OfArray(new[]
            {
                new Fs.Address("456 Oak Ave", "Portland", Microsoft.FSharp.Core.FSharpOption<string>.None),
                new Fs.Address("789 Pine Rd", "Seattle", Microsoft.FSharp.Core.FSharpOption<string>.Some("98101"))
            }),
            metadata: Microsoft.FSharp.Collections.MapModule.OfArray(new[]
            {
                new System.Tuple<string, string>("team", "platform"),
                new System.Tuple<string, string>("level", "senior"),
                new System.Tuple<string, string>("office", "remote")
            }),
            ratings: Microsoft.FSharp.Collections.MapModule.OfArray(new[]
            {
                new System.Tuple<string, int>("code_quality", 9),
                new System.Tuple<string, int>("communication", 8),
                new System.Tuple<string, int>("reliability", 10)
            }),
            rating: Microsoft.FSharp.Core.FSharpOption<double>.Some(4.7),
            contact: Fs.UserProfileContact.NewEmail("alice@example.com")
        );
    }

    [GlobalSetup]
    public void Setup()
    {
        _json = _formatter.Format(_csProfile);
    }

    [Benchmark(Description = "C# JSON Encode")]
    public string CSharp_JsonEncode() => _formatter.Format(_csProfile);

    [Benchmark(Description = "F# JSON Encode")]
    public string FSharp_JsonEncode() => Fs.UserProfileModule.encodeJson(_fsProfile);

    [Benchmark(Description = "C# JSON Decode")]
    public Cs.UserProfile CSharp_JsonDecode() => _parser.Parse<Cs.UserProfile>(_json);

    [Benchmark(Description = "F# JSON Decode")]
    public Fs.UserProfile FSharp_JsonDecode() => Fs.UserProfileModule.decodeJson(_json);
}
