using Google.Protobuf;

using Xunit;

using Cs = Interop;
using Fs = global::InteropFSharp;

namespace Grpc.FSharp.Integration.Tests;

/// <summary>
/// Cross-language integration tests: encode in C# (standard protobuf) and decode in F#, and vice versa.
/// Verifies wire-format compatibility between the F# generated code and the canonical C# protobuf implementation.
/// </summary>
public class InteropTests
{
    // -------------------------------------------------------
    // Person: basic string + int32
    // -------------------------------------------------------

    [Fact]
    public void Person_CSharpEncode_FSharpDecode()
    {
        var csPerson = new Cs.Person { Name = "Alice", Age = 30 };
        var bytes = csPerson.ToByteArray();

        var fsPerson = Fs.PersonModule.decode(bytes);

        Assert.Equal("Alice", fsPerson.Name);
        Assert.Equal(30, fsPerson.Age);
    }

    [Fact]
    public void Person_FSharpEncode_CSharpDecode()
    {
        var fsPerson = new Fs.Person("Alice", 30);
        var bytes = Fs.PersonModule.encode(fsPerson);

        var csPerson = Cs.Person.Parser.ParseFrom(bytes);

        Assert.Equal("Alice", csPerson.Name);
        Assert.Equal(30, csPerson.Age);
    }

    [Fact]
    public void Person_Roundtrip_FSharp()
    {
        var original = new Fs.Person("Bob", 42);
        var bytes = Fs.PersonModule.encode(original);
        var decoded = Fs.PersonModule.decode(bytes);

        Assert.Equal(original.Name, decoded.Name);
        Assert.Equal(original.Age, decoded.Age);
    }

    [Fact]
    public void Person_Empty_Roundtrip()
    {
        var csEmpty = new Cs.Person();
        var bytes = csEmpty.ToByteArray();

        var fsPerson = Fs.PersonModule.decode(bytes);

        Assert.Equal("", fsPerson.Name);
        Assert.Equal(0, fsPerson.Age);
    }

    // -------------------------------------------------------
    // ScalarTypes: all protobuf scalar types
    // -------------------------------------------------------

    [Fact]
    public void ScalarTypes_CSharpEncode_FSharpDecode()
    {
        var cs = new Cs.ScalarTypes
        {
            DoubleField = 3.14,
            FloatField = 2.72f,
            Int32Field = -42,
            Int64Field = -123456789L,
            Uint32Field = 42u,
            Uint64Field = 123456789UL,
            Sint32Field = -100,
            Sint64Field = -200L,
            Fixed32Field = 999u,
            Fixed64Field = 888UL,
            Sfixed32Field = -777,
            Sfixed64Field = -666L,
            BoolField = true,
            StringField = "hello protobuf",
            BytesField = ByteString.CopyFromUtf8("binary data"),
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.ScalarTypesModule.decode(bytes);

        Assert.Equal(3.14, fs.DoubleField, 5);
        Assert.Equal(2.72f, fs.FloatField, 2);
        Assert.Equal(-42, fs.Int32Field);
        Assert.Equal(-123456789L, fs.Int64Field);
        Assert.Equal(42u, fs.Uint32Field);
        Assert.Equal(123456789UL, fs.Uint64Field);
        Assert.Equal(-100, fs.Sint32Field);
        Assert.Equal(-200L, fs.Sint64Field);
        Assert.Equal(999u, fs.Fixed32Field);
        Assert.Equal(888UL, fs.Fixed64Field);
        Assert.Equal(-777, fs.Sfixed32Field);
        Assert.Equal(-666L, fs.Sfixed64Field);
        Assert.True(fs.BoolField);
        Assert.Equal("hello protobuf", fs.StringField);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("binary data"), fs.BytesField);
    }

    [Fact]
    public void ScalarTypes_FSharpEncode_CSharpDecode()
    {
        var fs = new Fs.ScalarTypes(
            3.14, 2.72f, -42, -123456789L, 42u, 123456789UL,
            -100, -200L, 999u, 888UL, -777, -666L,
            true, "hello protobuf",
            System.Text.Encoding.UTF8.GetBytes("binary data"));
        var bytes = Fs.ScalarTypesModule.encode(fs);

        var cs = Cs.ScalarTypes.Parser.ParseFrom(bytes);

        Assert.Equal(3.14, cs.DoubleField, 5);
        Assert.Equal(2.72f, cs.FloatField, 2);
        Assert.Equal(-42, cs.Int32Field);
        Assert.Equal(-123456789L, cs.Int64Field);
        Assert.Equal(42u, cs.Uint32Field);
        Assert.Equal(123456789UL, cs.Uint64Field);
        Assert.Equal(-100, cs.Sint32Field);
        Assert.Equal(-200L, cs.Sint64Field);
        Assert.Equal(999u, cs.Fixed32Field);
        Assert.Equal(888UL, cs.Fixed64Field);
        Assert.Equal(-777, cs.Sfixed32Field);
        Assert.Equal(-666L, cs.Sfixed64Field);
        Assert.True(cs.BoolField);
        Assert.Equal("hello protobuf", cs.StringField);
        Assert.Equal(ByteString.CopyFromUtf8("binary data"), cs.BytesField);
    }

    [Fact]
    public void ScalarTypes_DefaultValues_AreOmitted()
    {
        var fs = Fs.ScalarTypesModule.empty;
        var bytes = Fs.ScalarTypesModule.encode(fs);

        // Proto3: default values should produce zero bytes
        Assert.Empty(bytes);
    }

    // -------------------------------------------------------
    // Address: optional fields
    // -------------------------------------------------------

    [Fact]
    public void Address_WithOptional_CSharpEncode_FSharpDecode()
    {
        var cs = new Cs.Address
        {
            Street = "123 Main St",
            City = "Springfield",
            ZipCode = "62701",
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.AddressModule.decode(bytes);

        Assert.Equal("123 Main St", fs.Street);
        Assert.Equal("Springfield", fs.City);
        Assert.True(Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(fs.ZipCode));
        Assert.Equal("62701", fs.ZipCode.Value);
    }

    [Fact]
    public void Address_WithoutOptional_CSharpEncode_FSharpDecode()
    {
        var cs = new Cs.Address
        {
            Street = "456 Elm St",
            City = "Shelbyville",
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.AddressModule.decode(bytes);

        Assert.Equal("456 Elm St", fs.Street);
        Assert.Equal("Shelbyville", fs.City);
        Assert.True(Microsoft.FSharp.Core.FSharpOption<string>.get_IsNone(fs.ZipCode));
    }

    [Fact]
    public void Address_FSharpEncode_CSharpDecode()
    {
        var fs = new Fs.Address(
            "123 Main St", "Springfield",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("62701"));
        var bytes = Fs.AddressModule.encode(fs);

        var cs = Cs.Address.Parser.ParseFrom(bytes);

        Assert.Equal("123 Main St", cs.Street);
        Assert.Equal("Springfield", cs.City);
        Assert.True(cs.HasZipCode);
        Assert.Equal("62701", cs.ZipCode);
    }

    [Fact]
    public void Address_FSharpEncode_NoOptional_CSharpDecode()
    {
        var fs = new Fs.Address(
            "456 Elm St", "Shelbyville",
            Microsoft.FSharp.Core.FSharpOption<string>.None);
        var bytes = Fs.AddressModule.encode(fs);

        var cs = Cs.Address.Parser.ParseFrom(bytes);

        Assert.Equal("456 Elm St", cs.Street);
        Assert.Equal("Shelbyville", cs.City);
        Assert.False(cs.HasZipCode);
    }

    // -------------------------------------------------------
    // UserProfile: enum, message, repeated, map, oneof
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_Full_CSharpEncode_FSharpDecode()
    {
        var cs = new Cs.UserProfile
        {
            Id = "user-1",
            DisplayName = "Alice",
            Status = Cs.Status.Active,
            HomeAddress = new Cs.Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "62701",
            },
            Tags = { "admin", "developer" },
            Scores = { 10, 20, 30 },
            OtherAddresses =
            {
                new Cs.Address { Street = "Work St", City = "WorkCity" }
            },
            Metadata = { { "role", "admin" }, { "team", "platform" } },
            Ratings = { { "skill", 5 }, { "speed", 8 } },
            Rating = 4.5,
            PhoneNumber = "+1-555-1234",
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.UserProfileModule.decode(bytes);

        Assert.Equal("user-1", fs.Id);
        Assert.Equal("Alice", fs.DisplayName);
        Assert.Equal(Fs.Status.StatusActive, fs.Status);

        Assert.True(Microsoft.FSharp.Core.FSharpOption<Fs.Address>.get_IsSome(fs.HomeAddress));
        Assert.Equal("123 Main St", fs.HomeAddress.Value.Street);
        Assert.Equal("Springfield", fs.HomeAddress.Value.City);
        Assert.Equal("62701", fs.HomeAddress.Value.ZipCode.Value);

        var tags = Microsoft.FSharp.Collections.ListModule.ToArray(fs.Tags);
        Assert.Equal(new[] { "admin", "developer" }, tags);

        var scores = Microsoft.FSharp.Collections.ListModule.ToArray(fs.Scores);
        Assert.Equal(new[] { 10, 20, 30 }, scores);

        var addrs = Microsoft.FSharp.Collections.ListModule.ToArray(fs.OtherAddresses);
        Assert.Single(addrs);
        Assert.Equal("Work St", addrs[0].Street);

        Assert.Equal("admin", fs.Metadata["role"]);
        Assert.Equal("platform", fs.Metadata["team"]);

        Assert.Equal(5, fs.Ratings["skill"]);
        Assert.Equal(8, fs.Ratings["speed"]);

        Assert.True(Microsoft.FSharp.Core.FSharpOption<double>.get_IsSome(fs.Rating));
        Assert.Equal(4.5, fs.Rating.Value, 5);

        Assert.True(Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.get_IsSome(fs.Contact));
        Assert.IsType<Fs.UserProfileContact.PhoneNumber>(fs.Contact.Value);
        var phone = (Fs.UserProfileContact.PhoneNumber)fs.Contact.Value;
        Assert.Equal("+1-555-1234", phone.phoneNumber);
    }

    [Fact]
    public void UserProfile_Full_FSharpEncode_CSharpDecode()
    {
        var homeAddr = new Fs.Address(
            "123 Main St", "Springfield",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("62701"));

        var workAddr = new Fs.Address(
            "Work St", "WorkCity",
            Microsoft.FSharp.Core.FSharpOption<string>.None);

        var metadata = Microsoft.FSharp.Collections.MapModule.OfArray(
            new[] {
                new System.Tuple<string, string>("role", "admin"),
                new System.Tuple<string, string>("team", "platform"),
            });

        var ratings = Microsoft.FSharp.Collections.MapModule.OfArray(
            new[] {
                new System.Tuple<string, int>("skill", 5),
                new System.Tuple<string, int>("speed", 8),
            });

        var fs = new Fs.UserProfile(
            "user-1",
            "Alice",
            Fs.Status.StatusActive,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.Some(homeAddr),
            Microsoft.FSharp.Collections.ListModule.OfArray(new[] { "admin", "developer" }),
            Microsoft.FSharp.Collections.ListModule.OfArray(new[] { 10, 20, 30 }),
            Microsoft.FSharp.Collections.ListModule.OfArray(new[] { workAddr }),
            metadata,
            ratings,
            Microsoft.FSharp.Core.FSharpOption<double>.Some(4.5),
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.Some(Fs.UserProfileContact.NewPhoneNumber("+1-555-1234")));

        var bytes = Fs.UserProfileModule.encode(fs);

        var cs = Cs.UserProfile.Parser.ParseFrom(bytes);

        Assert.Equal("user-1", cs.Id);
        Assert.Equal("Alice", cs.DisplayName);
        Assert.Equal(Cs.Status.Active, cs.Status);

        Assert.Equal("123 Main St", cs.HomeAddress.Street);
        Assert.Equal("Springfield", cs.HomeAddress.City);
        Assert.Equal("62701", cs.HomeAddress.ZipCode);

        Assert.Equal(new[] { "admin", "developer" }, cs.Tags);
        Assert.Equal(new[] { 10, 20, 30 }, cs.Scores);

        Assert.Single(cs.OtherAddresses);
        Assert.Equal("Work St", cs.OtherAddresses[0].Street);

        Assert.Equal("admin", cs.Metadata["role"]);
        Assert.Equal("platform", cs.Metadata["team"]);
        Assert.Equal(5, cs.Ratings["skill"]);
        Assert.Equal(8, cs.Ratings["speed"]);

        Assert.Equal(4.5, cs.Rating, 5);
        Assert.Equal(Cs.UserProfile.ContactOneofCase.PhoneNumber, cs.ContactCase);
        Assert.Equal("+1-555-1234", cs.PhoneNumber);
    }

    [Fact]
    public void UserProfile_EmailOneof_CSharpEncode_FSharpDecode()
    {
        var cs = new Cs.UserProfile
        {
            Id = "user-2",
            Email = "alice@example.com",
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.UserProfileModule.decode(bytes);

        Assert.Equal("user-2", fs.Id);
        Assert.True(Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.get_IsSome(fs.Contact));
        Assert.IsType<Fs.UserProfileContact.Email>(fs.Contact.Value);
        var email = (Fs.UserProfileContact.Email)fs.Contact.Value;
        Assert.Equal("alice@example.com", email.email);
    }

    [Fact]
    public void UserProfile_EmailOneof_FSharpEncode_CSharpDecode()
    {
        var fs = new Fs.UserProfile(
            "user-2", "", Fs.Status.StatusUnknown,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.Empty<string>(),
            Microsoft.FSharp.Collections.ListModule.Empty<int>(),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, string>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, int>(),
            Microsoft.FSharp.Core.FSharpOption<double>.None,
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.Some(Fs.UserProfileContact.NewEmail("alice@example.com")));
        var bytes = Fs.UserProfileModule.encode(fs);

        var cs = Cs.UserProfile.Parser.ParseFrom(bytes);

        Assert.Equal("user-2", cs.Id);
        Assert.Equal(Cs.UserProfile.ContactOneofCase.Email, cs.ContactCase);
        Assert.Equal("alice@example.com", cs.Email);
    }

    [Fact]
    public void UserProfile_Minimal_Roundtrip()
    {
        var cs = new Cs.UserProfile { Id = "minimal" };
        var bytes = cs.ToByteArray();

        var fs = Fs.UserProfileModule.decode(bytes);
        Assert.Equal("minimal", fs.Id);
        Assert.Equal("", fs.DisplayName);
        Assert.Equal(Fs.Status.StatusUnknown, fs.Status);

        var reEncoded = Fs.UserProfileModule.encode(fs);
        var cs2 = Cs.UserProfile.Parser.ParseFrom(reEncoded);
        Assert.Equal("minimal", cs2.Id);
    }

    // -------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------

    [Fact]
    public void EmptyMessage_Roundtrips()
    {
        var cs = new Cs.Person();
        var bytes = cs.ToByteArray();

        var fs = Fs.PersonModule.decode(bytes);
        Assert.Equal("", fs.Name);
        Assert.Equal(0, fs.Age);

        var reEncoded = Fs.PersonModule.encode(fs);
        var cs2 = Cs.Person.Parser.ParseFrom(reEncoded);
        Assert.Equal("", cs2.Name);
        Assert.Equal(0, cs2.Age);
    }

    [Fact]
    public void Enum_AllValues_Roundtrip()
    {
        foreach (var status in new[] { Cs.Status.Unknown, Cs.Status.Active, Cs.Status.Inactive })
        {
            var cs = new Cs.UserProfile { Id = "enum-test", Status = status };
            var bytes = cs.ToByteArray();
            var fs = Fs.UserProfileModule.decode(bytes);
            Assert.Equal((int)status, (int)fs.Status);

            var reEncoded = Fs.UserProfileModule.encode(fs);
            var cs2 = Cs.UserProfile.Parser.ParseFrom(reEncoded);
            Assert.Equal(status, cs2.Status);
        }
    }

    [Fact]
    public void RepeatedScalar_Packed_DecodesInFSharp()
    {
        var cs = new Cs.UserProfile
        {
            Id = "packed-test",
            Scores = { 1, 2, 3, 4, 5 },
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.UserProfileModule.decode(bytes);
        var scores = Microsoft.FSharp.Collections.ListModule.ToArray(fs.Scores);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, scores);
    }

    [Fact]
    public void LargeStrings_Roundtrip()
    {
        var longString = new string('x', 10_000);
        var cs = new Cs.Person { Name = longString, Age = 1 };
        var bytes = cs.ToByteArray();

        var fs = Fs.PersonModule.decode(bytes);
        Assert.Equal(longString, fs.Name);

        var reEncoded = Fs.PersonModule.encode(fs);
        var cs2 = Cs.Person.Parser.ParseFrom(reEncoded);
        Assert.Equal(longString, cs2.Name);
    }

    [Fact]
    public void Unicode_Roundtrip()
    {
        var cs = new Cs.Person { Name = "\u3053\u3093\u306b\u3061\u306f\u4e16\u754c", Age = 1 };
        var bytes = cs.ToByteArray();

        var fs = Fs.PersonModule.decode(bytes);
        Assert.Equal("\u3053\u3093\u306b\u3061\u306f\u4e16\u754c", fs.Name);

        var reEncoded = Fs.PersonModule.encode(fs);
        var cs2 = Cs.Person.Parser.ParseFrom(reEncoded);
        Assert.Equal("\u3053\u3093\u306b\u3061\u306f\u4e16\u754c", cs2.Name);
    }

    [Fact]
    public void NegativeIntegers_Roundtrip()
    {
        var cs = new Cs.ScalarTypes
        {
            Int32Field = int.MinValue,
            Int64Field = long.MinValue,
            Sint32Field = int.MinValue,
            Sint64Field = long.MinValue,
            Sfixed32Field = int.MinValue,
            Sfixed64Field = long.MinValue,
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.ScalarTypesModule.decode(bytes);
        Assert.Equal(int.MinValue, fs.Int32Field);
        Assert.Equal(long.MinValue, fs.Int64Field);
        Assert.Equal(int.MinValue, fs.Sint32Field);
        Assert.Equal(long.MinValue, fs.Sint64Field);
        Assert.Equal(int.MinValue, fs.Sfixed32Field);
        Assert.Equal(long.MinValue, fs.Sfixed64Field);

        var reEncoded = Fs.ScalarTypesModule.encode(fs);
        var cs2 = Cs.ScalarTypes.Parser.ParseFrom(reEncoded);
        Assert.Equal(int.MinValue, cs2.Int32Field);
        Assert.Equal(long.MinValue, cs2.Int64Field);
    }

    [Fact]
    public void MaxValues_Roundtrip()
    {
        var cs = new Cs.ScalarTypes
        {
            Uint32Field = uint.MaxValue,
            Uint64Field = ulong.MaxValue,
            Fixed32Field = uint.MaxValue,
            Fixed64Field = ulong.MaxValue,
            DoubleField = double.MaxValue,
            FloatField = float.MaxValue,
        };
        var bytes = cs.ToByteArray();

        var fs = Fs.ScalarTypesModule.decode(bytes);
        Assert.Equal(uint.MaxValue, fs.Uint32Field);
        Assert.Equal(ulong.MaxValue, fs.Uint64Field);
        Assert.Equal(uint.MaxValue, fs.Fixed32Field);
        Assert.Equal(ulong.MaxValue, fs.Fixed64Field);
        Assert.Equal(double.MaxValue, fs.DoubleField);
        Assert.Equal(float.MaxValue, fs.FloatField);
    }
}
