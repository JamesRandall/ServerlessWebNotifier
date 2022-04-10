namespace WebNotifier.Lambda

open System
open Amazon.Lambda.CloudWatchEvents
open Amazon.Lambda.CloudWatchEvents.ScheduledEvents
open Amazon.Lambda.Core
open Amazon.S3
open Amazon.S3.Model
open Amazon.SecretsManager
open Amazon.SecretsManager.Model
open Twilio
open Twilio.Rest.Api.V2010.Account 
open Twilio.Types
open Flurl.Http

[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()

type WebNotifierFunction() =
  member __.FunctionHandlerAsync (event:CloudWatchEvent<ScheduledEvent>) (context: ILambdaContext) = task {
    let s3Client = new AmazonS3Client()
    let bucketName = DotEnv.getEnvironmentVariable "bucketname"
    let mobilePhoneNumber = DotEnv.getEnvironmentVariable "mobilenumber"
    let linkShortenerUrlOption = DotEnv.getEnvironmentVariableOption "linkshortenerurl"
    let linkShortenerApiKeySecretIdOption = DotEnv.getEnvironmentVariableOption "linkshortenerapikeysecretid"
    use secretsClient = new AmazonSecretsManagerClient()
    
    context.Logger.Log $"BUCKET NAME: {bucketName}\n"
    let shortenLink link = async {
      try
        match linkShortenerApiKeySecretIdOption, linkShortenerUrlOption with
        | Some secretId, Some linkShortenerUrl ->
          let! secret = secretsClient.GetSecretValueAsync(GetSecretValueRequest(SecretId=secretId)) |> Async.AwaitTask
          let apiKey = secret.SecretString
          let! shortlink =
            linkShortenerUrl
              .WithHeaders({| x_api_key = apiKey |}, true)
              .PostStringAsync(link)
              .ReceiveString()
              |> Async.AwaitTask
          return Ok shortlink
        | _ -> return Ok link
      with
      | exn -> return Error exn.Message
    }
    let getFile filename = async {
      let! snapshot = s3Client.GetObjectAsync (bucketName, filename) |> Async.AwaitTask
      use reader = new System.IO.StreamReader(snapshot.ResponseStream)
      let! text = reader.ReadToEndAsync() |> Async.AwaitTask
      return Ok text
    }
    let saveFile isPublic filename content = async {
      do!
        s3Client.PutObjectAsync(
          PutObjectRequest(
            BucketName = bucketName,
            Key = filename,
            ContentBody = content
          )
        )
        |> Async.AwaitTask |> Async.Ignore
      if isPublic then
        let presignedUrl =
          s3Client.GetPreSignedURL(
            GetPreSignedUrlRequest(
              BucketName = bucketName,
              Key = filename,
              Expires = System.DateTime.UtcNow.AddDays 7
            )
          )
        return! presignedUrl |> shortenLink
      else
        return Ok filename
    }
    let notify message = async {
      let! twilioTokenSecret = secretsClient.GetSecretValueAsync(GetSecretValueRequest(SecretId="webnotifier/twilioAuthTokenSecret")) |> Async.AwaitTask
      let twilioSid = DotEnv.getEnvironmentVariable "twilioSid"
      let messagingServiceSid = DotEnv.getEnvironmentVariable "twilioMessageServiceSid"
            
      TwilioClient.Init(twilioSid, twilioTokenSecret.SecretString)
      let messageOptions =
        CreateMessageOptions(
          PhoneNumber(mobilePhoneNumber),
          MessagingServiceSid = messagingServiceSid,
          Body = message
        )
      let! message = MessageResource.CreateAsync messageOptions |> Async.AwaitTask
      match message.ErrorCode.HasValue, String.IsNullOrWhiteSpace(message.ErrorMessage) with
      | true, false -> return Error $"Twilio error code {message.ErrorCode}: No additional error info"
      | true, true -> return Error $"Twilio error code {message.ErrorCode}: {message.ErrorMessage}" 
      | _ -> return Ok ()
    }
    
    context.Logger.LogLine("Beginning web notifier lambda")
    
    do!
      WebNotifier.execute
        context.Logger.LogLine
        getFile
        saveFile
        notify
      |> Async.Ignore

    context.Logger.LogLine("Ended web notifier lambda")
  }