using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Google.Protobuf;

using Cs = Interop;
using Fs = InteropFSharp;

BenchmarkRunner.Run(typeof(Program).Assembly);

[MemoryDiagnoser]
public class PersonBenchmarks
{
    private readonly Cs.Person _csPerson = new() { Name = "Alice Johnson", Age = 42 };
    private readonly Fs.Person _fsPerson = new("Alice Johnson", 42);
    private byte[] _encodedBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _encodedBytes = _csPerson.ToByteArray();
    }

    [Benchmark(Description = "C# Encode")]
    public byte[] CSharp_Encode() => _csPerson.ToByteArray();

    [Benchmark(Description = "F# Encode")]
    public byte[] FSharp_Encode() => Fs.PersonModule.encode(_fsPerson);

    [Benchmark(Description = "C# Decode")]
    public Cs.Person CSharp_Decode() => Cs.Person.Parser.ParseFrom(_encodedBytes);

    [Benchmark(Description = "F# Decode")]
    public Fs.Person FSharp_Decode() => Fs.PersonModule.decode(_encodedBytes);
}

[MemoryDiagnoser]
public class ScalarTypesBenchmarks
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

    private byte[] _encodedBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _encodedBytes = _csScalars.ToByteArray();
    }

    [Benchmark(Description = "C# Encode")]
    public byte[] CSharp_Encode() => _csScalars.ToByteArray();

    [Benchmark(Description = "F# Encode")]
    public byte[] FSharp_Encode() => Fs.ScalarTypesModule.encode(_fsScalars);

    [Benchmark(Description = "C# Decode")]
    public Cs.ScalarTypes CSharp_Decode() => Cs.ScalarTypes.Parser.ParseFrom(_encodedBytes);

    [Benchmark(Description = "F# Decode")]
    public Fs.ScalarTypes FSharp_Decode() => Fs.ScalarTypesModule.decode(_encodedBytes);
}

[MemoryDiagnoser]
public class UserProfileBenchmarks
{
    private readonly Cs.UserProfile _csProfile;
    private readonly Fs.UserProfile _fsProfile;
    private byte[] _encodedBytes = null!;

    public UserProfileBenchmarks()
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
        _encodedBytes = _csProfile.ToByteArray();
    }

    [Benchmark(Description = "C# Encode")]
    public byte[] CSharp_Encode() => _csProfile.ToByteArray();

    [Benchmark(Description = "F# Encode")]
    public byte[] FSharp_Encode() => Fs.UserProfileModule.encode(_fsProfile);

    [Benchmark(Description = "C# Decode")]
    public Cs.UserProfile CSharp_Decode() => Cs.UserProfile.Parser.ParseFrom(_encodedBytes);

    [Benchmark(Description = "F# Decode")]
    public Fs.UserProfile FSharp_Decode() => Fs.UserProfileModule.decode(_encodedBytes);
}
