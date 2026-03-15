using Google.Protobuf;

using Xunit;

using Fs = global::InteropFSharp;

namespace FSharp.Grpc.Integration.Tests;

/// <summary>
/// F# roundtrip property tests: encode → decode → re-encode, assert bytes1 == bytes2 (idempotent).
/// </summary>
public class RoundtripTests
{
    // -------------------------------------------------------
    // Person
    // -------------------------------------------------------

    [Fact]
    public void Person_Roundtrip_Idempotent()
    {
        var original = new Fs.Person("Bob", 42);
        var bytes1 = Fs.PersonModule.encode(original);
        var decoded = Fs.PersonModule.decode(bytes1);
        var bytes2 = Fs.PersonModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Person_Empty_Roundtrip_Idempotent()
    {
        var original = Fs.PersonModule.empty;
        var bytes1 = Fs.PersonModule.encode(original);
        var decoded = Fs.PersonModule.decode(bytes1);
        var bytes2 = Fs.PersonModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Person_LargeString_Roundtrip_Idempotent()
    {
        var original = new Fs.Person(new string('z', 10_000), 99);
        var bytes1 = Fs.PersonModule.encode(original);
        var decoded = Fs.PersonModule.decode(bytes1);
        var bytes2 = Fs.PersonModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    // -------------------------------------------------------
    // ScalarTypes
    // -------------------------------------------------------

    [Fact]
    public void ScalarTypes_Full_Roundtrip_Idempotent()
    {
        var original = new Fs.ScalarTypes(
            3.14, 2.72f, -42, -123456789L, 42u, 123456789UL,
            -100, -200L, 999u, 888UL, -777, -666L,
            true, "hello", System.Text.Encoding.UTF8.GetBytes("data"));
        var bytes1 = Fs.ScalarTypesModule.encode(original);
        var decoded = Fs.ScalarTypesModule.decode(bytes1);
        var bytes2 = Fs.ScalarTypesModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void ScalarTypes_Empty_Roundtrip_Idempotent()
    {
        var original = Fs.ScalarTypesModule.empty;
        var bytes1 = Fs.ScalarTypesModule.encode(original);
        var decoded = Fs.ScalarTypesModule.decode(bytes1);
        var bytes2 = Fs.ScalarTypesModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void ScalarTypes_ExtremeValues_Roundtrip_Idempotent()
    {
        var original = new Fs.ScalarTypes(
            double.MaxValue, float.MaxValue,
            int.MinValue, long.MinValue,
            uint.MaxValue, ulong.MaxValue,
            int.MinValue, long.MinValue,
            uint.MaxValue, ulong.MaxValue,
            int.MinValue, long.MinValue,
            true, "extreme", new byte[] { 0xFF, 0x00, 0xAB });
        var bytes1 = Fs.ScalarTypesModule.encode(original);
        var decoded = Fs.ScalarTypesModule.decode(bytes1);
        var bytes2 = Fs.ScalarTypesModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    // -------------------------------------------------------
    // Address
    // -------------------------------------------------------

    [Fact]
    public void Address_WithOptional_Roundtrip_Idempotent()
    {
        var original = new Fs.Address(
            "123 Main St", "Springfield",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("62701"));
        var bytes1 = Fs.AddressModule.encode(original);
        var decoded = Fs.AddressModule.decode(bytes1);
        var bytes2 = Fs.AddressModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Address_WithoutOptional_Roundtrip_Idempotent()
    {
        var original = new Fs.Address(
            "456 Elm St", "Shelbyville",
            Microsoft.FSharp.Core.FSharpOption<string>.None);
        var bytes1 = Fs.AddressModule.encode(original);
        var decoded = Fs.AddressModule.decode(bytes1);
        var bytes2 = Fs.AddressModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Address_Empty_Roundtrip_Idempotent()
    {
        var original = Fs.AddressModule.empty;
        var bytes1 = Fs.AddressModule.encode(original);
        var decoded = Fs.AddressModule.decode(bytes1);
        var bytes2 = Fs.AddressModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    // -------------------------------------------------------
    // UserProfile
    // -------------------------------------------------------

    [Fact]
    public void UserProfile_Full_Roundtrip_Idempotent()
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

        var original = new Fs.UserProfile(
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

        var bytes1 = Fs.UserProfileModule.encode(original);
        var decoded = Fs.UserProfileModule.decode(bytes1);
        var bytes2 = Fs.UserProfileModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void UserProfile_EmailOneof_Roundtrip_Idempotent()
    {
        var original = new Fs.UserProfile(
            "user-2", "", Fs.Status.StatusUnknown,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.Empty<string>(),
            Microsoft.FSharp.Collections.ListModule.Empty<int>(),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, string>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, int>(),
            Microsoft.FSharp.Core.FSharpOption<double>.None,
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.Some(Fs.UserProfileContact.NewEmail("alice@example.com")));
        var bytes1 = Fs.UserProfileModule.encode(original);
        var decoded = Fs.UserProfileModule.decode(bytes1);
        var bytes2 = Fs.UserProfileModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void UserProfile_Minimal_Roundtrip_Idempotent()
    {
        var original = new Fs.UserProfile(
            "minimal", "", Fs.Status.StatusUnknown,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.Empty<string>(),
            Microsoft.FSharp.Collections.ListModule.Empty<int>(),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, string>(),
            Microsoft.FSharp.Collections.MapModule.Empty<string, int>(),
            Microsoft.FSharp.Core.FSharpOption<double>.None,
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.None);
        var bytes1 = Fs.UserProfileModule.encode(original);
        var decoded = Fs.UserProfileModule.decode(bytes1);
        var bytes2 = Fs.UserProfileModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void UserProfile_ManyItems_Roundtrip_Idempotent()
    {
        var scores = new int[50];
        for (int i = 0; i < 50; i++) scores[i] = i * 7;
        var tags = new string[15];
        for (int i = 0; i < 15; i++) tags[i] = $"tag-{i}";

        var metaPairs = new System.Tuple<string, string>[10];
        for (int i = 0; i < 10; i++)
            metaPairs[i] = new System.Tuple<string, string>($"key-{i}", $"value-{i}");

        var original = new Fs.UserProfile(
            "scale", "Test User", Fs.Status.StatusActive,
            Microsoft.FSharp.Core.FSharpOption<Fs.Address>.None,
            Microsoft.FSharp.Collections.ListModule.OfArray(tags),
            Microsoft.FSharp.Collections.ListModule.OfArray(scores),
            Microsoft.FSharp.Collections.ListModule.Empty<Fs.Address>(),
            Microsoft.FSharp.Collections.MapModule.OfArray(metaPairs),
            Microsoft.FSharp.Collections.MapModule.Empty<string, int>(),
            Microsoft.FSharp.Core.FSharpOption<double>.Some(9.99),
            Microsoft.FSharp.Core.FSharpOption<Fs.UserProfileContact>.Some(Fs.UserProfileContact.NewPhoneNumber("+1-000-0000")));
        var bytes1 = Fs.UserProfileModule.encode(original);
        var decoded = Fs.UserProfileModule.decode(bytes1);
        var bytes2 = Fs.UserProfileModule.encode(decoded);

        Assert.Equal(bytes1, bytes2);
    }
}
