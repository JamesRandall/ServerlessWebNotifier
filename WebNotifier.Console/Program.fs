open System
open System.IO
open WebNotifier

let bucketName = "working"
let defaultColor = Console.ForegroundColor

let getFile filename = async {
  let resolvedFilename =
    if filename = "config.json" then filename else $"{bucketName}/{filename}" 
  return Ok (File.ReadAllText resolvedFilename)
}
let saveFile isPublic filename (content:string) = async {
  if not (Directory.Exists bucketName) then Directory.CreateDirectory bucketName |> ignore
  do! File.WriteAllTextAsync ($"{bucketName}/{filename}",content) |> Async.AwaitTask
  return Ok $"{bucketName}/{filename}"
}
let notify (message:string) = async {
  Console.ForegroundColor <- ConsoleColor.Green
  Console.WriteLine $"NOTIFY: {message}"
  Console.ForegroundColor <- defaultColor
  return Ok ()
}

execute
  (fun m ->
    Console.ForegroundColor <- ConsoleColor.Blue
    Console.WriteLine m
    Console.ForegroundColor <- defaultColor
  )
  getFile
  saveFile
  notify
|> Async.RunSynchronously
|> ignore