using Google.Protobuf;

using Xunit;

using Cs = Interop;
using Fs = global::InteropFSharp;

namespace Grpc.FSharp.Integration.Tests;

/// <summary>
/// Byte-exact encoding verification: asserts that F# encoded bytes == C# encoded bytes
/// for all message types and edge cases.
/// </summary>
public class EncodingVerificationTests
{
    // -------------------------------------------------------
    // Person: simple message (2 fields)
    // -------------------------------------------------------

    [Fact]
    public void Person_ByteExact()
    {
        var cs = new Cs.Person { Name = "Alice", Age = 30 };
        var csBytes = cs.ToByteArray();

        var fs = new Fs.Person("Alice", 30);
        var fsBytes = Fs.PersonModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // ScalarTypes: all 15 scalar field types populated
    // -------------------------------------------------------

    [Fact]
    public void ScalarTypes_AllFields_ByteExact()
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
        var csBytes = cs.ToByteArray();

        var fs = new Fs.ScalarTypes(
            3.14, 2.72f, -42, -123456789L, 42u, 123456789UL,
            -100, -200L, 999u, 888UL, -777, -666L,
            true, "hello protobuf",
            System.Text.Encoding.UTF8.GetBytes("binary data"));
        var fsBytes = Fs.ScalarTypesModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // ScalarTypes: empty (zero bytes)
    // -------------------------------------------------------

    [Fact]
    public void ScalarTypes_Empty_ByteExact()
    {
        var cs = new Cs.ScalarTypes();
        var csBytes = cs.ToByteArray();

        var fs = Fs.ScalarTypesModule.empty;
        var fsBytes = Fs.ScalarTypesModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
        Assert.Empty(fsBytes);
    }

    // -------------------------------------------------------
    // Address: with optional field present
    // -------------------------------------------------------

    [Fact]
    public void Address_WithOptional_ByteExact()
    {
        var cs = new Cs.Address
        {
            Street = "123 Main St",
            City = "Springfield",
            ZipCode = "62701",
        };
        var csBytes = cs.ToByteArray();

        var fs = new Fs.Address(
            "123 Main St", "Springfield",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("62701"));
        var fsBytes = Fs.AddressModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // Address: with optional field absent
    // -------------------------------------------------------

    [Fact]
    public void Address_WithoutOptional_ByteExact()
    {
        var cs = new Cs.Address
        {
            Street = "456 Elm St",
            City = "Shelbyville",
        };
        var csBytes = cs.ToByteArray();

        var fs = new Fs.Address(
            "456 Elm St", "Shelbyville",
            Microsoft.FSharp.Core.FSharpOption<string>.None);
        var fsBytes = Fs.AddressModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // UserProfile: fully populated
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_Full_ByteExact()
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
        var csBytes = cs.ToByteArray();

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

        var fsBytes = Fs.UserProfileModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // UserProfile: with email oneof (alternative case)
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_EmailOneof_ByteExact()
    {
        var cs = new Cs.UserProfile
        {
            Id = "user-2",
            Email = "alice@example.com",
        };
        var csBytes = cs.ToByteArray();

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
        var fsBytes = Fs.UserProfileModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // UserProfile: minimal (only id set)
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_Minimal_ByteExact()
    {
        var cs = new Cs.UserProfile { Id = "minimal" };
        var csBytes = cs.ToByteArray();

        var fs = new Fs.UserProfile(
            "minimal", "", Fs.Status.StatusUnknown,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.Empty<string>(),
            Microsoft.FSharp.Collections.ListModule.Empty<int>(),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, string>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, int>(),
            Microsoft.FSharp.Core.FSharpOption<double>.None,
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.None);
        var fsBytes = Fs.UserProfileModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // Person: large string (10K chars)
    // -------------------------------------------------------

    [Fact]
    public void Person_LargeString_ByteExact()
    {
        var longString = new string('x', 10_000);
        var cs = new Cs.Person { Name = longString, Age = 1 };
        var csBytes = cs.ToByteArray();

        var fs = new Fs.Person(longString, 1);
        var fsBytes = Fs.PersonModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // ScalarTypes: negative min values (varint edge cases)
    // -------------------------------------------------------

    [Fact]
    public void ScalarTypes_NegativeMinValues_ByteExact()
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
        var csBytes = cs.ToByteArray();

        var fs = new Fs.ScalarTypes(
            0.0, 0.0f, int.MinValue, long.MinValue, 0u, 0UL,
            int.MinValue, long.MinValue, 0u, 0UL, int.MinValue, long.MinValue,
            false, "", System.Array.Empty<byte>());
        var fsBytes = Fs.ScalarTypesModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // ScalarTypes: max unsigned values (varint edge cases)
    // -------------------------------------------------------

    [Fact]
    public void ScalarTypes_MaxUnsignedValues_ByteExact()
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
        var csBytes = cs.ToByteArray();

        var fs = new Fs.ScalarTypes(
            double.MaxValue, float.MaxValue, 0, 0L, uint.MaxValue, ulong.MaxValue,
            0, 0L, uint.MaxValue, ulong.MaxValue, 0, 0L,
            false, "", System.Array.Empty<byte>());
        var fsBytes = Fs.ScalarTypesModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // UserProfile: many repeated items (packed encoding at scale)
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_ManyRepeated_ByteExact()
    {
        var cs = new Cs.UserProfile { Id = "scale-test" };
        for (int i = 0; i < 50; i++) cs.Scores.Add(i * 7);
        for (int i = 0; i < 15; i++) cs.Tags.Add($"tag-{i}");
        var csBytes = cs.ToByteArray();

        var scores = new int[50];
        for (int i = 0; i < 50; i++) scores[i] = i * 7;
        var tags = new string[15];
        for (int i = 0; i < 15; i++) tags[i] = $"tag-{i}";

        var fs = new Fs.UserProfile(
            "scale-test", "", Fs.Status.StatusUnknown,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.OfArray(tags),
            Microsoft.FSharp.Collections.ListModule.OfArray(scores),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, string>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, int>(),
            Microsoft.FSharp.Core.FSharpOption<double>.None,
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.None);
        var fsBytes = Fs.UserProfileModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
    }

    // -------------------------------------------------------
    // UserProfile: many map entries (map encoding at scale)
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_ManyMapEntries_RoundtripVerified()
    {
        // Map entry ordering is undefined in protobuf, so we verify via decode instead of byte equality.
        var metaPairs = new System.Tuple<string, string>[15];
        for (int i = 0; i < 15; i++)
            metaPairs[i] = new System.Tuple<string, string>($"key-{i}", $"value-{i}");
        var ratingPairs = new System.Tuple<string, int>[12];
        for (int i = 0; i < 12; i++)
            ratingPairs[i] = new System.Tuple<string, int>($"rating-{i}", i * 3);

        var fs = new Fs.UserProfile(
            "map-test", "", Fs.Status.StatusUnknown,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.Empty<string>(),
            Microsoft.FSharp.Collections.ListModule.Empty<int>(),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.OfArray(metaPairs),
            Microsoft.FSharp.Collections.MapModule.OfArray(ratingPairs),
            Microsoft.FSharp.Core.FSharpOption<double>.None,
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.None);
        var fsBytes = Fs.UserProfileModule.encode(fs);

        // Verify C# can decode F# bytes correctly
        var cs = Cs.UserProfile.Parser.ParseFrom(fsBytes);
        Assert.Equal("map-test", cs.Id);
        Assert.Equal(15, cs.Metadata.Count);
        for (int i = 0; i < 15; i++)
            Assert.Equal($"value-{i}", cs.Metadata[$"key-{i}"]);
        Assert.Equal(12, cs.Ratings.Count);
        for (int i = 0; i < 12; i++)
            Assert.Equal(i * 3, cs.Ratings[$"rating-{i}"]);

        // Verify F# roundtrip is idempotent
        var decoded = Fs.UserProfileModule.decode(fsBytes);
        var reEncoded = Fs.UserProfileModule.encode(decoded);
        Assert.Equal(fsBytes, reEncoded);
    }

    // -------------------------------------------------------
    // Address: empty string fields (all defaults, zero bytes)
    // -------------------------------------------------------

    [Fact]
    public void Address_EmptyFields_ByteExact()
    {
        var cs = new Cs.Address();
        var csBytes = cs.ToByteArray();

        var fs = Fs.AddressModule.empty;
        var fsBytes = Fs.AddressModule.encode(fs);

        Assert.Equal(csBytes, fsBytes);
        Assert.Empty(fsBytes);
    }
}
