namespace WebNotifierDeploy

open Amazon.CDK
open Amazon.CDK.AWS
open Amazon.CDK.AWS.Events
open Amazon.CDK.AWS.Lambda
open Amazon.CDK.AWS.S3.Deployment

type WebNotifierDeployStack(scope, id, props) as this =
    inherit Stack(scope, id, props)
    
    let bucketName = CfnParameter(this, "bucketName", CfnParameterProps(Default="webnotifier2"))
    let twilioSid = CfnParameter(this, "twilioSid")
    let twilioMessageServiceSid = CfnParameter(this, "twilioMessageGroupSid")
    let mobileNumber = CfnParameter(this, "mobileNumber")
    let linkShortenerUrl = CfnParameter(this, "linkShortenerUrl")
    let linkShortenerSecretId = CfnParameter(this, "linkShortenerSecretId", CfnParameterProps(Default="linkshortener/apikey"))
    let twilioAuthTokenSecret = SecretsManager.Secret.FromSecretNameV2(this, "twilioAuthTokenSecret", "webnotifier/twilioAuthTokenSecret")
    let linkShortenerSecret = SecretsManager.Secret.FromSecretNameV2(this, "linkShortenerSecret", linkShortenerSecretId.ValueAsString)

    let bucket = S3.Bucket(this, "webnotifierbucket", S3.BucketProps(BucketName=bucketName.ValueAsString))
    do
      BucketDeployment(
        this,
        "deployconfig",
        BucketDeploymentProps(
          Sources = [|Source.Asset("./config")|],
          DestinationBucket = bucket
        )
      )
      |> ignore
    
    let lambdaRole =
      IAM.Role(
        this,
        "webnotifierrole",
        IAM.RoleProps(
          AssumedBy = IAM.ServicePrincipal("lambda.amazonaws.com"),
          ManagedPolicies = [|
            IAM.ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole")
            IAM.ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole")
          |]
        )
      )
    do twilioAuthTokenSecret.GrantRead(lambdaRole) |> ignore
    do linkShortenerSecret.GrantRead(lambdaRole) |> ignore
    do bucket.GrantReadWrite(lambdaRole) |> ignore
    
    let lambda = Function(
      this,
      "webnotifier-lambda-function",
      FunctionProps(
        Runtime = Runtime.DOTNET_6,
        Code = Code.FromAsset("../WebNotifier.Lambda/output.zip"),
        Handler = "WebNotifier.Lambda::WebNotifier.Lambda.WebNotifierFunction::FunctionHandlerAsync",
        Environment = ([
          "bucketname", bucketName.ValueAsString
          "mobilenumber" , mobileNumber.ValueAsString
          "twilioSid", twilioSid.ValueAsString
          "twilioMessageServiceSid", twilioMessageServiceSid.ValueAsString
          "linkshortenerurl", linkShortenerUrl.ValueAsString
          "linkshortenerapikeysecretid", linkShortenerSecretId.ValueAsString
        ] |> Map.ofList),
        Role = lambdaRole,
        Timeout = Duration.Minutes(4)
      )  
    )
    
    let scheduleRule = Rule(
      this,
      "webnotifier-lambda-schedule",
      RuleProps(
        Schedule = Schedule.Cron(CronOptions(Minute = "0/5"))
      )
    )
    do scheduleRule.AddTarget (Targets.LambdaFunction(lambda))
