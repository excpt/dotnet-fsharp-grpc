module Grpc.FSharp.Tools.Tests.FabulousAstTests

open System.IO
open Xunit
open Fabulous.AST
open type Fabulous.AST.Ast

let private generate node = node |> Gen.mkOak |> Gen.run

let private expectedDir =
    let asmDir =
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

    Path.Combine(asmDir, "expected")

let private assertMatchesExpected (name: string) (actual: string) =
    let path = Path.Combine(expectedDir, $"{name}.fs.expected")
    let expected = File.ReadAllText(path).ReplaceLineEndings("\n")
    let normalised = actual.ReplaceLineEndings("\n")
    Assert.Equal(expected, normalised)

// -------------------------------------------------------------
// Records
// -------------------------------------------------------------

[<Fact>]
let ``simple record with fields`` () =
    Oak() {
        AnonymousModule() {
            Record("User") {
                Field("Name", String())
                Field("Age", Int())
            }
        }
    }
    |> generate
    |> assertMatchesExpected "SimpleRecord"

[<Fact>]
let ``record with summary xmlDocs`` () =
    Oak() {
        AnonymousModule() {
            Record("User") { Field("Name", String()) }
            |> _.xmlDocs([ "<summary>Represents a user in the system.</summary>" ])
        }
    }
    |> generate
    |> assertMatchesExpected "RecordWithSummary"

[<Fact>]
let ``record with multi-line summary xmlDocs`` () =
    Oak() {
        AnonymousModule() {
            Record("User") { Field("Name", String()) }
            |> _.xmlDocs([ "<summary>"; "Represents a user."; "Used in auth."; "</summary>" ])
        }
    }
    |> generate
    |> assertMatchesExpected "RecordWithMultiLineSummary"

[<Fact>]
let ``record field with xmlDocs`` () =
    Oak() {
        AnonymousModule() {
            Record("User") {
                Field("Name", String()) |> _.xmlDocs([ "<summary>The user's name.</summary>" ])
                Field("Age", Int())
            }
        }
    }
    |> generate
    |> assertMatchesExpected "RecordFieldWithXmlDocs"

[<Fact>]
let ``record with static member`` () =
    Oak() {
        AnonymousModule() {
            (Record("User") { Field("Name", String()) }).members () {
                Member("Empty", RecordExpr([ RecordFieldExpr("Name", String("")) ])).toStatic ()
            }
        }
    }
    |> generate
    |> assertMatchesExpected "RecordWithStaticMember"

[<Fact>]
let ``mutually recursive records`` () =
    Oak() {
        AnonymousModule() {
            Record("Folder") {
                Field("Name", String())
                Field("Items", LongIdent("Item list"))
            }

            Record("Item") {
                Field("ItemName", String())
                Field("SubFolder", LongIdent("Folder option"))
            }
            |> _.toRecursive()
        }
    }
    |> generate
    |> assertMatchesExpected "MutuallyRecursiveRecords"

// -------------------------------------------------------------
// Enums
// -------------------------------------------------------------

[<Fact>]
let ``enum with cases`` () =
    Oak() {
        AnonymousModule() {
            Enum("Status") {
                EnumCase("Unknown", Int(0))
                EnumCase("Active", Int(1))
            }
        }
    }
    |> generate
    |> assertMatchesExpected "EnumWithCases"

[<Fact>]
let ``enum with xmlDocs on type and cases`` () =
    Oak() {
        AnonymousModule() {
            Enum("Status") {
                EnumCase("Unknown", Int(0)) |> _.xmlDocs([ "<summary>Default.</summary>" ])

                EnumCase("Active", Int(1))
                |> _.xmlDocs([ "<summary>Resource is active.</summary>" ])
            }
            |> _.xmlDocs([ "<summary>Status of a resource.</summary>" ])
        }
    }
    |> generate
    |> assertMatchesExpected "EnumWithXmlDocs"

// -------------------------------------------------------------
// Discriminated Unions
// -------------------------------------------------------------

[<Fact>]
let ``discriminated union with RequireQualifiedAccess`` () =
    Oak() {
        AnonymousModule() {
            Union("PaymentMethod") {
                UnionCase("CreditCard", [ Field("card", LongIdent "CreditCard") ])
                UnionCase("BankTransfer", [ Field("transfer", LongIdent "BankTransfer") ])
            }
            |> _.attribute(Attribute("RequireQualifiedAccess"))
        }
    }
    |> generate
    |> assertMatchesExpected "DiscriminatedUnion"

// -------------------------------------------------------------
// Namespace
// -------------------------------------------------------------

[<Fact>]
let ``namespace wraps types`` () =
    Oak() { Namespace("Example.Users") { Record("User") { Field("Name", String()) } } }
    |> generate
    |> assertMatchesExpected "NamespaceWrapsTypes"

// -------------------------------------------------------------
// Obsolete attribute
// -------------------------------------------------------------

[<Fact>]
let ``record with Obsolete attribute`` () =
    Oak() {
        AnonymousModule() {
            Record("LegacyUser") { Field("Name", String()) }
            |> _.attribute(Attribute("System.Obsolete", ParenExpr(String("This message is deprecated."))))
        }
    }
    |> generate
    |> assertMatchesExpected "RecordWithObsolete"
