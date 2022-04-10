module WebNotifier
  open System
  open HtmlAgilityPack
  open Thoth.Json.Net
  
  module AsyncResult =
    let bind handler result = async {
      match! result with
      | Ok value -> return! handler value
      | Error e -> return Error e
    }
    
    let map handler result = async {
      match! result with
      | Ok value ->
        let! returnValue = handler value
        return Ok returnValue
      | Error e -> return Error e
    }
    
    let onOk handler resultAsync = async {
      match! resultAsync with
      | Ok result ->
        handler ()
        return Ok result
      | Error e -> return Error e
    }
    
    let defaultOnError defaultValue resultAsync = async {
      match! resultAsync with
      | Ok s -> return Ok s
      | Error _ -> return Ok defaultValue
    }
  
  type Detector =
    | Difference
  
  type Action =
    { XPathSelector: string
      Detector: Detector
    }
  
  type WebsiteNotification =
    { Name: string
      Uri: string
      Actions: Action list
    }
    
  type ActionResultInfo =
    { Action: Action
      ChangeDetected: bool
    }
  type ActionErrorResultInfo =
    { Action: Action
      Error: string
    }
    
  type WebsiteResult =
    { Name: string
      ActionResults: ActionResultInfo list
      ErrorResults: ActionErrorResultInfo list
      ChangeDetected: bool
    }
    
  let toWebsiteResults (website:WebsiteNotification) resultsAsync = async {
    let! results = resultsAsync
    return
      { Name = website.Name
        ActionResults =
          results
          |> Seq.mapi(fun actionIndex result ->
            match result with
            | Ok changeDetected ->
              [{ Action = website.Actions.[actionIndex] ; ChangeDetected = changeDetected}]
            | _ -> []
          )
          |> List.concat
        ErrorResults =
          results
          |> Seq.mapi(fun actionIndex result ->
            match result with
            | Error e ->
              [{ Action = website.Actions.[actionIndex] ; Error = e }]
            | _ -> []
          )
          |> List.concat
        ChangeDetected = results |> Seq.tryFind(function | Ok true -> true | _ -> false) |> Option.isSome
      }
      
  }
  
  let snapshotFilename = "snapshot.txt"
  let configFilename = "config.json"
  
  let getTextContent (document:HtmlDocument) (selectorXPath:string) =
    let content = document.DocumentNode.SelectNodes selectorXPath
    content
    |> Seq.map(fun outerNode ->
      outerNode.DescendantsAndSelf()
      |> Seq.map(fun node ->
        if node.NodeType = HtmlNodeType.Text then
          $"{node.InnerText.Trim()}\n"
        else
          ""
      )
    )
    |> Seq.concat
    |> String.Concat
  
  
  
  let execute logger getFile saveFile notify = async {
    let getExistingSnapshot () = async {
      try
        return! getFile snapshotFilename
      with
        | exn -> return Error exn.Message 
    }
  
    let upsertSnapshot content = async {
      let datedFilename = DateTime.UtcNow.ToString("yyyy-MM-dd_HH:mm:ss")
      do! saveFile false snapshotFilename content |> Async.Ignore
      let! endpointResult = saveFile true $"{datedFilename}.txt" content
      return endpointResult
    }
    
    let getConfiguration () = async {
      let! contentResult = getFile configFilename
      return
        contentResult
        |> Result.bind (fun content ->
          Decode.Auto.fromString<WebsiteNotification list> (content,caseStrategy=CaseStrategy.CamelCase)
        )
    }
    
    let executeAction htmlDocument websiteIndex (websiteConfiguration:WebsiteNotification) actionIndex action  = async {
      match action.Detector with
      | Difference ->
        return!
          getExistingSnapshot ()
          |> AsyncResult.defaultOnError "" 
          |> AsyncResult.bind(fun existingSnapshot -> async {
            let newSnapshot = getTextContent htmlDocument action.XPathSelector
            if newSnapshot <> existingSnapshot then
              return!
                upsertSnapshot newSnapshot
                |> AsyncResult.bind (fun newSnapshotUrl -> async {
                  logger $"Change detected for {websiteConfiguration.Name} action {actionIndex}"
                  do! notify $"UPDATE: {websiteConfiguration.Name}\n{newSnapshotUrl}" |> Async.Ignore
                  return Ok true
                })
            else
              logger $"No change detected for {websiteConfiguration.Name} action {actionIndex}"
              return Ok false
          })
    } 
    
    let executeWebsite index (website:WebsiteNotification) = async {
      let! document = HtmlWeb().LoadFromWebAsync website.Uri |> Async.AwaitTask
      return!
        website.Actions
        |> List.mapi (executeAction document index website)
        |> Async.Sequential
        |> toWebsiteResults website
    }
    
    let! configurationResult = getConfiguration ()
    match configurationResult with
    | Ok configuration ->
      logger "Configuration loaded"
      let! results =
        configuration
        |> List.mapi executeWebsite
        |> Async.Sequential
      return Ok results
    | Error e ->
      logger e
      return Error e
  }
  
  