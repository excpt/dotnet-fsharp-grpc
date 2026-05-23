module FSharp.Grpc.Tools.Tests.PipelineTests

open System
open System.IO
open Xunit
open FsUnit.Xunit
open FSharp.Grpc.Tools.Pipeline
open FSharp.Grpc.Tools.ProtocResolver

let private findProtocOrFail () =
    findProtoc ()
    |> Option.defaultWith (fun () -> failwith "protoc not found — install Grpc.Tools NuGet package")

let private wktPathOrFail () =
    findWellKnownTypesPath ()
    |> Option.defaultWith (fun () -> failwith "well-known types path not found — install Grpc.Tools NuGet package")

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

let private runPipeline (dir: string) (protos: string list) =
    run
        { Protoc = findProtocOrFail ()
          ProtoPaths = [ dir; wktPathOrFail () ]
          OutputDir = dir
          ProtoFiles = protos
          GrpcServices = None }

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

        let result = runPipeline dir [ "a.proto"; "z.proto" ]
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
            runPipeline dir [ "audit.proto"; "transactions.proto"; "accounts.proto" ]

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
            runPipeline dir [ "orders.proto"; "customers.proto"; "products.proto"; "common.proto" ]

        let manifest = File.ReadAllText(result.ManifestPath)
        let commonIdx = indexOf manifest "common.generated.fs"
        let customersIdx = indexOf manifest "customers.generated.fs"
        let productsIdx = indexOf manifest "products.generated.fs"
        let ordersIdx = indexOf manifest "orders.generated.fs"

        commonIdx |> should be (lessThan customersIdx)
        commonIdx |> should be (lessThan productsIdx)
        customersIdx |> should be (lessThan ordersIdx)
        productsIdx |> should be (lessThan ordersIdx))
