module FSharp.Grpc.Tools.Tests.PipelineTests

open System
open System.IO
open System.Runtime.InteropServices
open Xunit
open FsUnit.Xunit
open FSharp.Grpc.Tools.Pipeline
open FSharp.Grpc.Tools.ProtocResolver

// A fresh clone won't have Grpc.Tools cached under ~/.nuget until the first restore,
// so skip rather than fail when the resolver returns None — the test corpus is gated
// on having protoc and the well-known types available, not on a test-logic concern.
let private findProtocOrSkip () =
    findProtoc ()
    |> Option.defaultWith (fun () ->
        Assert.Skip("protoc not found in ~/.nuget/packages/grpc.tools — run `dotnet restore` to prime the cache")
        Unchecked.defaultof<_>)

let private wktPathOrSkip () =
    findWellKnownTypesPath ()
    |> Option.defaultWith (fun () ->
        Assert.Skip(
            "well-known types not found in ~/.nuget/packages/grpc.tools — run `dotnet restore` to prime the cache"
        )

        Unchecked.defaultof<_>)

let private withTempDir (f: string -> unit) =
    let dir =
        Path.Combine(Path.GetTempPath(), $"fsharp-grpc-pipeline-{Guid.NewGuid():N}")

    Directory.CreateDirectory(dir) |> ignore

    try
        f dir
    finally
        if Directory.Exists dir then
            Directory.Delete(dir, true)

let private writeProto (dir: string) (name: string) (body: string) =
    File.WriteAllText(Path.Combine(dir, name), body.TrimStart('\n'))

let private spec (file: string) (services: GrpcServices) : ProtoSpec = { File = file; Services = services }

let private runPipeline (dir: string) (protos: ProtoSpec list) =
    run
        { Protoc = findProtocOrSkip ()
          ProtoPaths = [ dir; wktPathOrSkip () ]
          OutputDir = dir
          Protos = protos }
    |> function
        | Ok r -> r
        | Error msg -> failwithf "pipeline failed: %s" msg

let private messagesOnly (files: string list) =
    files |> List.map (fun f -> spec f None)

let private indexOf (manifest: string) (path: string) =
    let i = manifest.IndexOf(path: string)

    if i < 0 then
        failwithf "%s not found in manifest:\n%s" path manifest

    i

// ---------------------------------------------------------
// (1) Simplest case — name actively fights alphabetical order
// ---------------------------------------------------------

[<Fact>]
let ``manifest puts dependency before dependent when names are anti-alphabetical`` () =
    // a.proto imports z.proto. Alphabetical: a before z. Topo: z before a.
    withTempDir (fun dir ->
        writeProto
            dir
            "z.proto"
            """
syntax = "proto3";
package zpkg;
message ZValue { string v = 1; }
"""

        writeProto
            dir
            "a.proto"
            """
syntax = "proto3";
package apkg;
import "z.proto";
message AValue { zpkg.ZValue z = 1; }
"""

        let result = runPipeline dir (messagesOnly [ "a.proto"; "z.proto" ])
        let manifest = File.ReadAllText(result.ManifestPath)

        indexOf manifest "z.generated.fs"
        |> should be (lessThan (indexOf manifest "a.generated.fs")))

// ---------------------------------------------------------
// (2) Transitive chain with realistic names
// ---------------------------------------------------------

[<Fact>]
let ``manifest orders transitive chain by import graph not file name`` () =
    // Realistic chain: audit.proto -> transactions.proto -> accounts.proto.
    // Alphabetical: accounts, audit, transactions — audit ends up before transactions, which is wrong.
    // Topo: accounts, transactions, audit.
    withTempDir (fun dir ->
        writeProto
            dir
            "accounts.proto"
            """
syntax = "proto3";
package banking;
message Account { string iban = 1; string holder = 2; }
"""

        writeProto
            dir
            "transactions.proto"
            """
syntax = "proto3";
package banking;
import "accounts.proto";
message Transaction {
  string id = 1;
  banking.Account source = 2;
  banking.Account destination = 3;
  int64 amount_cents = 4;
}
"""

        writeProto
            dir
            "audit.proto"
            """
syntax = "proto3";
package banking;
import "transactions.proto";
message AuditEntry {
  string actor = 1;
  banking.Transaction txn = 2;
}
"""

        let result =
            runPipeline dir (messagesOnly [ "audit.proto"; "transactions.proto"; "accounts.proto" ])

        let manifest = File.ReadAllText(result.ManifestPath)
        let accountsIdx = indexOf manifest "accounts.generated.fs"
        let txnIdx = indexOf manifest "transactions.generated.fs"
        let auditIdx = indexOf manifest "audit.generated.fs"

        accountsIdx |> should be (lessThan txnIdx)
        txnIdx |> should be (lessThan auditIdx))

// ---------------------------------------------------------
// (3) Diamond — two paths converge on a shared root
// ---------------------------------------------------------

[<Fact>]
let ``manifest orders diamond dependency before either branch`` () =
    // orders.proto imports customers.proto and products.proto.
    // customers.proto and products.proto both import common.proto.
    // Alphabetical: common, customers, orders, products — orders lands before products, which it depends on.
    // Topo: common first, then customers and products (any order), then orders last.
    withTempDir (fun dir ->
        writeProto
            dir
            "common.proto"
            """
syntax = "proto3";
package shop;
message Money { string currency = 1; int64 cents = 2; }
"""

        writeProto
            dir
            "customers.proto"
            """
syntax = "proto3";
package shop;
import "common.proto";
message Customer {
  string id = 1;
  shop.Money lifetime_value = 2;
}
"""

        writeProto
            dir
            "products.proto"
            """
syntax = "proto3";
package shop;
import "common.proto";
message Product {
  string sku = 1;
  shop.Money price = 2;
}
"""

        writeProto
            dir
            "orders.proto"
            """
syntax = "proto3";
package shop;
import "customers.proto";
import "products.proto";
message Order {
  shop.Customer customer = 1;
  repeated shop.Product items = 2;
}
"""

        let result =
            runPipeline dir (messagesOnly [ "orders.proto"; "customers.proto"; "products.proto"; "common.proto" ])

        let manifest = File.ReadAllText(result.ManifestPath)
        let commonIdx = indexOf manifest "common.generated.fs"
        let customersIdx = indexOf manifest "customers.generated.fs"
        let productsIdx = indexOf manifest "products.generated.fs"
        let ordersIdx = indexOf manifest "orders.generated.fs"

        commonIdx |> should be (lessThan customersIdx)
        commonIdx |> should be (lessThan productsIdx)
        customersIdx |> should be (lessThan ordersIdx)
        productsIdx |> should be (lessThan ordersIdx))

// ---------------------------------------------------------
// (4) Mixed per-item GrpcServices in one project
// ---------------------------------------------------------

[<Fact>]
let ``per-item GrpcServices emits only requested stubs`` () =
    // Bounded contexts: this project hosts an Orders server and consumes Inventory as a client.
    // No cross-imports — independent protos with different stub flavors.
    withTempDir (fun dir ->
        writeProto
            dir
            "orders.proto"
            """
syntax = "proto3";
package orders;
message Order { string id = 1; }
service OrdersApi {
  rpc Get(Order) returns (Order);
}
"""

        writeProto
            dir
            "inventory.proto"
            """
syntax = "proto3";
package inventory;
message Stock { string sku = 1; int32 qty = 2; }
service InventoryApi {
  rpc Lookup(Stock) returns (Stock);
}
"""

        let result =
            runPipeline dir [ spec "orders.proto" Server; spec "inventory.proto" Client ]

        let exists name = File.Exists(Path.Combine(dir, name))

        // Orders: messages + server stub, no client stub
        exists "orders.generated.fs" |> should be True
        exists "orders.server.generated.fs" |> should be True
        exists "orders.client.generated.fs" |> should be False

        // Inventory: messages + client stub, no server stub
        exists "inventory.generated.fs" |> should be True
        exists "inventory.client.generated.fs" |> should be True
        exists "inventory.server.generated.fs" |> should be False)

// ---------------------------------------------------------
// (5) Proto path with a literal " in it
// ---------------------------------------------------------

[<Fact>]
let ``run handles a proto-path containing a literal double-quote`` () =
    // The previous protoc argv was built by interpolating each path inside "..." — a literal "
    // in a directory name closed the quoting prematurely and broke --proto_path. ArgumentList.Add
    // hands each value to the OS as a single argv element with no shell-style escaping required.
    // Skipped on Windows since NTFS rejects " in filenames.
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        ()
    else
        withTempDir (fun outerDir ->
            let dirWithQuote = Path.Combine(outerDir, "has\"quote")
            Directory.CreateDirectory(dirWithQuote) |> ignore

            writeProto
                dirWithQuote
                "foo.proto"
                """
syntax = "proto3";
package quoted;
message Foo { string v = 1; }
"""

            let result =
                run
                    { Protoc = findProtocOrSkip ()
                      ProtoPaths = [ dirWithQuote; wktPathOrSkip () ]
                      OutputDir = outerDir
                      Protos = [ spec "foo.proto" None ] }

            match result with
            | Ok r -> r.MessageFiles |> List.length |> should equal 1
            | Error msg -> failwithf "expected Ok, got Error: %s" msg)

// ---------------------------------------------------------
// (6) protoc failure surfaces as Error, not an exception
// ---------------------------------------------------------

[<Fact>]
let ``run returns Error when protoc fails to resolve a proto file`` () =
    // No proto written — protoc can't find missing.proto on the proto-path and exits non-zero.
    // The pipeline must surface that as Error, not failwith.
    withTempDir (fun dir ->
        let result =
            run
                { Protoc = findProtocOrSkip ()
                  ProtoPaths = [ dir; wktPathOrSkip () ]
                  OutputDir = dir
                  Protos = [ spec "missing.proto" None ] }

        match result with
        | Ok r -> failwithf "expected Error, got Ok %A" r
        | Error msg -> msg |> should haveSubstring "missing.proto")
