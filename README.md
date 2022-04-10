# Web Notifier

This is a pretty simple serverless change detector for websites that will regularly check a web page and see if it has been updated and if so send you a text message. Useful for, you know, getting your hands on things like a PS5! Mostly written just for fun.

## Prerequisites

To use this you will need:

* A Twilio account - you'll need your account SID, an auth token and a message service SID. The usual Twilio stuff.
* AWS CDK - deployment takes place through the CDK, remember to bootstrap it in your region.
* A bash / zsh shell - the deployment script uses Bash, would be a simple conversion process to use Powershell or something if for some mysterious reason Windows is your thing.

Optional but recommended:

* A link shortener - the notifier will share a link to the latest change set as part of the SMS message using an S3 presigned URL. These are really really really long and will burn through your SMS bill. If you deploy this [serverless link shortener](https://github.com/JamesRandall/ServerlessLinkShortener) as recommended in the instructions then you will just need to supply your domain name to the web notifier. 

I say the link shortener is optional but the current CDK script runs on the basis you have set this up. Couple of tweaks to the CDK will be needed to not use a shortener.

## Deployment

### Configuring the notifications

First thing to do is to configure the website(s) you want the system to monitor. Open ./WebDeploy.Config/config/config.json in your favourite text editor. It should look a bit like this:

    [
      {
        "name": "Independent PS5 Stock Checker",
        "uri": "https://www.independent.co.uk/games/ps5-stock-uk-restock-latest-b2048401.html",
        "actions": [
          {
            "xPathSelector": "//div[@items]",
            "detector": "Difference"
          }
        ]
     }
   ]

Essentially an array of web pages and an XPath telling the notifier what content to use to change check. The _detector_ must always be difference at the moment.

Once you've got that set up you're good to deploy.

### Deploying to AWS

Firstly you will need to create a secret for your Twilio auth token in Secrets Manager. The secret should be named webnotifier/twilioAuthTokenSecret and should be set as simple plain text (not JSONified).

Then to deploy go to the ./WebNotifier.Deploy folder and run the deploy.sh script. It takes 5 parameters:

    ./deploy.sh "{mobilenumber}" "{messageserviceid}" "{twiliosid}" "{linkshortenerurl}" "{s3bucketname}"

So it might look like this:

    ./deploy.sh "{+4412345874534}" "MG2323498fsdfds023" "AC73246324fdfddfs" "https://myshort.com" "webnotifier"

In theory that's it and every five minutes your websites will be checked for changes.

## Support

This comes with my I don't give two f^&%s support guarantee. I mean I might help. I might not. I have a life you know.



