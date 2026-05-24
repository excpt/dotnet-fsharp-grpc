using System;
using System.Collections.Generic;
using System.Linq;

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

using Google.Protobuf;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

using Cs = Interop;
using Fs = global::InteropFSharp;

namespace FSharp.Grpc.Integration.Tests;

/// <summary>
/// Property-based round-trip tests. For each shared message type we assert three
/// invariants over generated inputs:
///   1. F# encode → C# decode yields a Cs message with the same fields.
///   2. C# encode → F# decode yields an Fs record with the same fields.
///   3. F# encode → F# decode is the identity.
///
/// FsCheck's default generators are used for scalars; for floats/doubles we
/// reject NaN/Infinity per-property (NaN ≠ NaN under proto3 JSON / structural
/// equality, but binary preserves the bits). For string/bytes we reject null
/// (proto3 has no concept of a null string/bytes value).
/// </summary>
public class PropertyTests
{
    // ---------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------

    private static FSharpOption<T> Some<T>(T value) => FSharpOption<T>.Some(value);

    private static FSharpList<T> ToFList<T>(IEnumerable<T> items) =>
        ListModule.OfSeq(items);

    private static FSharpMap<TKey, TValue> ToFMap<TKey, TValue>(IEnumerable<Tuple<TKey, TValue>> kvps)
        where TKey : System.IComparable<TKey> =>
        MapModule.OfSeq(kvps);

    private static bool FloatBitsEqual(float a, float b) =>
        System.BitConverter.SingleToInt32Bits(a) == System.BitConverter.SingleToInt32Bits(b);

    private static bool DoubleBitsEqual(double a, double b) =>
        System.BitConverter.DoubleToInt64Bits(a) == System.BitConverter.DoubleToInt64Bits(b);

    // ---------------------------------------------------------
    // Person
    // ---------------------------------------------------------

    [Property]
    public bool Person_FSharpEncode_CSharpDecode(NonNull<string> name, int age)
    {
        var fs = new Fs.Person(name.Get, age);
        var bytes = Fs.PersonModule.encode(fs);
        var cs = Cs.Person.Parser.ParseFrom(bytes);
        return cs.Name == name.Get && cs.Age == age;
    }

    [Property]
    public bool Person_CSharpEncode_FSharpDecode(NonNull<string> name, int age)
    {
        var cs = new Cs.Person { Name = name.Get, Age = age };
        var bytes = cs.ToByteArray();
        var fs = Fs.PersonModule.decode(bytes);
        return fs.Name == name.Get && fs.Age == age;
    }

    [Property]
    public bool Person_FSharpRoundtrip(NonNull<string> name, int age)
    {
        var original = new Fs.Person(name.Get, age);
        var decoded = Fs.PersonModule.decode(Fs.PersonModule.encode(original));
        return decoded.Equals(original);
    }

    // ---------------------------------------------------------
    // ScalarTypes — full coverage of every proto3 scalar
    // ---------------------------------------------------------

    private static bool IsCleanDouble(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    private static bool IsCleanFloat(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    public record ScalarInputs(
        double D, float F, int I32, long I64, uint U32, ulong U64,
        int S32, long S64, uint Fix32, ulong Fix64, int SFix32, long SFix64,
        bool B, NonNull<string> S, NonNull<byte[]> Bytes);

    private static Fs.ScalarTypes ToFs(ScalarInputs i) =>
        new(i.D, i.F, i.I32, i.I64, i.U32, i.U64,
            i.S32, i.S64, i.Fix32, i.Fix64, i.SFix32, i.SFix64,
            i.B, i.S.Get, i.Bytes.Get);

    private static Cs.ScalarTypes ToCs(ScalarInputs i) =>
        new()
        {
            DoubleField = i.D,
            FloatField = i.F,
            Int32Field = i.I32,
            Int64Field = i.I64,
            Uint32Field = i.U32,
            Uint64Field = i.U64,
            Sint32Field = i.S32,
            Sint64Field = i.S64,
            Fixed32Field = i.Fix32,
            Fixed64Field = i.Fix64,
            Sfixed32Field = i.SFix32,
            Sfixed64Field = i.SFix64,
            BoolField = i.B,
            StringField = i.S.Get,
            BytesField = ByteString.CopyFrom(i.Bytes.Get),
        };

    [Property]
    public Property ScalarTypes_FSharpEncode_CSharpDecode(ScalarInputs i)
    {
        return (IsCleanDouble(i.D) && IsCleanFloat(i.F)).When(() =>
        {
            var fs = ToFs(i);
            var bytes = Fs.ScalarTypesModule.encode(fs);
            var cs = Cs.ScalarTypes.Parser.ParseFrom(bytes);
            return cs.DoubleField == i.D
                && cs.FloatField == i.F
                && cs.Int32Field == i.I32
                && cs.Int64Field == i.I64
                && cs.Uint32Field == i.U32
                && cs.Uint64Field == i.U64
                && cs.Sint32Field == i.S32
                && cs.Sint64Field == i.S64
                && cs.Fixed32Field == i.Fix32
                && cs.Fixed64Field == i.Fix64
                && cs.Sfixed32Field == i.SFix32
                && cs.Sfixed64Field == i.SFix64
                && cs.BoolField == i.B
                && cs.StringField == i.S.Get
                && cs.BytesField.ToByteArray().SequenceEqual(i.Bytes.Get);
        });
    }

    [Property]
    public Property ScalarTypes_CSharpEncode_FSharpDecode(ScalarInputs i)
    {
        return (IsCleanDouble(i.D) && IsCleanFloat(i.F)).When(() =>
        {
            var cs = ToCs(i);
            var bytes = cs.ToByteArray();
            var fs = Fs.ScalarTypesModule.decode(bytes);
            return fs.DoubleField == i.D
                && fs.FloatField == i.F
                && fs.Int32Field == i.I32
                && fs.Int64Field == i.I64
                && fs.Uint32Field == i.U32
                && fs.Uint64Field == i.U64
                && fs.Sint32Field == i.S32
                && fs.Sint64Field == i.S64
                && fs.Fixed32Field == i.Fix32
                && fs.Fixed64Field == i.Fix64
                && fs.Sfixed32Field == i.SFix32
                && fs.Sfixed64Field == i.SFix64
                && fs.BoolField == i.B
                && fs.StringField == i.S.Get
                && fs.BytesField.SequenceEqual(i.Bytes.Get);
        });
    }

    // NaN/Infinity-preserving round-trip via bit-level equality. Proto binary
    // wire format preserves the exact double bits, so the round-trip should be
    // bit-identical even for NaN payloads and signed zero.
    [Property]
    public bool ScalarTypes_Double_BitExact_Roundtrip(double d)
    {
        var fs = Fs.ScalarTypesModule.empty;
        fs = new Fs.ScalarTypes(d, fs.FloatField, fs.Int32Field, fs.Int64Field, fs.Uint32Field, fs.Uint64Field,
            fs.Sint32Field, fs.Sint64Field, fs.Fixed32Field, fs.Fixed64Field, fs.Sfixed32Field, fs.Sfixed64Field,
            fs.BoolField, fs.StringField, fs.BytesField);
        var bytes = Fs.ScalarTypesModule.encode(fs);
        var decoded = Fs.ScalarTypesModule.decode(bytes);
        return DoubleBitsEqual(decoded.DoubleField, d);
    }

    [Property]
    public bool ScalarTypes_Float_BitExact_Roundtrip(float f)
    {
        var fs = Fs.ScalarTypesModule.empty;
        fs = new Fs.ScalarTypes(fs.DoubleField, f, fs.Int32Field, fs.Int64Field, fs.Uint32Field, fs.Uint64Field,
            fs.Sint32Field, fs.Sint64Field, fs.Fixed32Field, fs.Fixed64Field, fs.Sfixed32Field, fs.Sfixed64Field,
            fs.BoolField, fs.StringField, fs.BytesField);
        var bytes = Fs.ScalarTypesModule.encode(fs);
        var decoded = Fs.ScalarTypesModule.decode(bytes);
        return FloatBitsEqual(decoded.FloatField, f);
    }

    // ---------------------------------------------------------
    // Address — covers proto3 optional
    // ---------------------------------------------------------

    // `NonNull<T>` is a reference-type wrapper, so `NonNull<string>?` is just a
    // nullable annotation — FsCheck's default arbitrary always produces a
    // non-null wrapper and never exercises the proto3 `optional` absent case.
    // Driving presence with an explicit `bool` guarantees both branches.

    [Property]
    public bool Address_FSharpEncode_CSharpDecode(
        NonNull<string> street, NonNull<string> city, bool zipPresent, NonNull<string> zip)
    {
        var fsZip = zipPresent ? Some(zip.Get) : FSharpOption<string>.None;
        var fs = new Fs.Address(street.Get, city.Get, fsZip);
        var bytes = Fs.AddressModule.encode(fs);
        var cs = Cs.Address.Parser.ParseFrom(bytes);
        return cs.Street == street.Get
            && cs.City == city.Get
            && cs.HasZipCode == zipPresent
            && (!zipPresent || cs.ZipCode == zip.Get);
    }

    [Property]
    public bool Address_CSharpEncode_FSharpDecode(
        NonNull<string> street, NonNull<string> city, bool zipPresent, NonNull<string> zip)
    {
        var cs = new Cs.Address { Street = street.Get, City = city.Get };
        if (zipPresent) cs.ZipCode = zip.Get;
        var bytes = cs.ToByteArray();
        var fs = Fs.AddressModule.decode(bytes);
        return fs.Street == street.Get
            && fs.City == city.Get
            && FSharpOption<string>.get_IsSome(fs.ZipCode) == zipPresent
            && (!zipPresent || fs.ZipCode.Value == zip.Get);
    }

    // ---------------------------------------------------------
    // UserProfile — repeated, map, oneof, optional, enum, message field
    // ---------------------------------------------------------

    public record UserProfileInputs(
        NonNull<string> Id, NonNull<string> Display, int StatusOrdinal,
        NonNull<string>[] Tags, int[] Scores,
        KeyValuePair<NonNull<string>, NonNull<string>>[] Metadata,
        KeyValuePair<NonNull<string>, int>[] Ratings,
        double? Rating, ContactChoice? Contact);

    public enum ContactChoice { Phone, Email }

    private static Fs.UserProfile ToFs(UserProfileInputs i, string contactText)
    {
        var status = (Fs.Status)System.Math.Abs(i.StatusOrdinal % 3);
        var rating = i.Rating is { } r && IsCleanDouble(r) ? Some(r) : FSharpOption<double>.None;

        // Presence comes from `i.Contact` (Nullable<ContactChoice> — value-type
        // nullable that FsCheck does generate both states for); `contactText` is
        // always supplied so each present case has a real string to round-trip.
        var contact = i.Contact switch
        {
            ContactChoice.Phone => Some(Fs.UserProfileContact.NewPhoneNumber(contactText)),
            ContactChoice.Email => Some(Fs.UserProfileContact.NewEmail(contactText)),
            _ => FSharpOption<Fs.UserProfileContact>.None,
        };

        // Dedupe map keys (a Map can't have duplicates and the wire format would
        // only emit the last writer-wins entry, breaking the equality assertion).
        var metaUnique = i.Metadata
            .GroupBy(kv => kv.Key.Get)
            .ToDictionary(g => g.Key, g => g.Last().Value.Get);
        var ratingsUnique = i.Ratings
            .GroupBy(kv => kv.Key.Get)
            .ToDictionary(g => g.Key, g => g.Last().Value);

        return new Fs.UserProfile(
            i.Id.Get,
            i.Display.Get,
            status,
            FSharpOption<Fs.Address>.None,
            ToFList(i.Tags.Select(t => t.Get)),
            ToFList<int>(i.Scores),
            FSharpList<Fs.Address>.Empty,
            ToFMap(metaUnique.Select(kv => Tuple.Create(kv.Key, kv.Value))),
            ToFMap(ratingsUnique.Select(kv => Tuple.Create(kv.Key, kv.Value))),
            rating,
            contact);
    }

    [Property]
    public bool UserProfile_FSharpEncode_CSharpDecode_Roundtrip(UserProfileInputs i, NonNull<string> contactText)
    {
        // Full cross-lang round-trip: F# encode → C# decode → C# re-encode → F# decode.
        // Byte equality is too strict — proto3 map entries can appear in any order on the
        // wire, so the C#-emitted bytes need not match the F#-emitted bytes verbatim.
        // Semantic equality of the round-tripped record is the strongest invariant here.
        var fs = ToFs(i, contactText.Get);
        var bytes = Fs.UserProfileModule.encode(fs);
        var cs = Cs.UserProfile.Parser.ParseFrom(bytes);
        var roundTripped = Fs.UserProfileModule.decode(cs.ToByteArray());
        return roundTripped.Equals(fs);
    }

    [Property]
    public bool UserProfile_FSharpRoundtrip(UserProfileInputs i, NonNull<string> contactText)
    {
        var original = ToFs(i, contactText.Get);
        var decoded = Fs.UserProfileModule.decode(Fs.UserProfileModule.encode(original));
        return decoded.Equals(original);
    }
}
