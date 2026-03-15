module ProtoFileStore

open System
open System.IO
open Store

let printTask (task: Task) =
    let status = if task.Completed then "done" else "todo"
    printfn $"  [%s{status}] %s{task.Id} — %s{task.Title} (priority: %A{task.Priority})"

    if task.Description <> "" then
        printfn $"         %s{task.Description}"

    match task.Assignee with
    | Some a -> printfn $"         assigned to: %s{a}"
    | None -> ()

    if not (List.isEmpty task.Tags) then
        let tagStr =
            task.Tags |> List.map (fun t -> $"{t.Key}={t.Value}") |> String.concat ", "

        printfn $"         tags: %s{tagStr}"

    if not (Map.isEmpty task.Metadata) then
        let metaStr =
            task.Metadata
            |> Map.toList
            |> List.map (fun (k, v) -> $"{k}={v}")
            |> String.concat ", "

        printfn $"         metadata: %s{metaStr}"

let printTaskList (taskList: TaskList) =
    printfn $"Task list: %s{taskList.Name} (%d{List.length taskList.Tasks} tasks)"
    taskList.Tasks |> List.iter printTask

[<EntryPoint>]
let main argv =
    let filePath =
        if argv.Length > 0 then
            argv.[0]
        else
            Path.Combine(Path.GetTempPath(), "sample-tasks.bin")

    // Build some sample data
    let tasks =
        { Name = "Sprint 12"
          Tasks =
            [ { Task.empty with
                  Id = "TASK-001"
                  Title = "Implement protobuf codegen"
                  Description = "Generate idiomatic F# records from .proto files"
                  Priority = Priority.PriorityHigh
                  Completed = true
                  Tags = [ { Key = "area"; Value = "codegen" }; { Key = "lang"; Value = "fsharp" } ]
                  Assignee = Some "alice"
                  Metadata = Map.ofList [ "sprint", "12"; "estimate", "8" ] }
              { Task.empty with
                  Id = "TASK-002"
                  Title = "Add gRPC service stubs"
                  Priority = Priority.PriorityMedium
                  Tags = [ { Key = "area"; Value = "grpc" } ]
                  Metadata = Map.ofList [ "sprint", "12" ] }
              { Task.empty with
                  Id = "TASK-003"
                  Title = "Write sample project"
                  Description = "Demonstrate file I/O with generated types"
                  Priority = Priority.PriorityLow
                  Completed = true
                  Assignee = Some "bob" } ] }

    // Encode and write to disk
    let bytes = TaskList.encode tasks
    File.WriteAllBytes(filePath, bytes)
    printfn $"Wrote %d{bytes.Length} bytes to %s{filePath}"
    printfn ""

    // Read back from disk and decode
    let loaded = File.ReadAllBytes(filePath) |> TaskList.decode
    printTaskList loaded
    printfn ""

    // Verify roundtrip
    let roundtripped = loaded |> TaskList.encode

    if bytes = roundtripped then
        printfn "Roundtrip OK — re-encoded bytes match original"
    else
        printfn $"Roundtrip MISMATCH — %d{bytes.Length} vs %d{roundtripped.Length} bytes"

    0
