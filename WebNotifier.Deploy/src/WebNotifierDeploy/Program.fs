open Amazon.CDK
open WebNotifierDeploy

[<EntryPoint>]
let main _ =
    let app = App(null)

    WebNotifierDeployStack(app, "WebNotifierDeployStack", StackProps()) |> ignore

    app.Synth() |> ignore
    0
