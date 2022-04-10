cd ../WebNotifier.Lambda
dotnet tool install -g Amazon.Lambda.Tools
dotnet lambda package -o output.zip
cd ../WebNotifier.Deploy
cdk deploy --parameters mobileNumber=$1 --parameters twilioMessageGroupSid=$2 --parameters twilioSid=$3 --parameters linkShortenerUrl=$4 --parameters bucketName=$5
rm ../WebNotifier.Lambda/output.zip
