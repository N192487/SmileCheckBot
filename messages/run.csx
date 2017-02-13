#r "Newtonsoft.Json"
#r "System.IO"

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Newtonsoft.Json;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    // Initialize the azure bot
    using (BotService.Initialize())
    {
        // Deserialize the incoming activity
        string jsonContent = await req.Content.ReadAsStringAsync();
        var activity = JsonConvert.DeserializeObject<Activity>(jsonContent);
        
        // authenticate incoming request and add activity.ServiceUrl to MicrosoftAppCredentials.TrustedHostNames
        // if request is authenticated
        if (!await BotService.Authenticator.TryAuthenticateAsync(req, new [] {activity}, CancellationToken.None))
        {
            return BotAuthenticator.GenerateUnauthorizedResponse(req);
        }
        
        if (activity != null)
        {
            await NewMethod(log, activity);
        }
        return req.CreateResponse(HttpStatusCode.Accepted);
    }    
}

async Task NewMethod(TraceWriter log, object activity)
{
    // one of these will have an interface and process it
    switch (activity.GetActivityType())
    {
        case ActivityTypes.Message:
            ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
            var responseMsg = "お疲れではありませんか。写真を送ってください。";

            if (activity.Attachments?.Any() == true)
            {
                foreach (var attachment in activity.Attachments)
                {
                    if (attachment.ContentType.Contains("image"))
                    {
                        var photoUrl = attachment.ContentUrl;

                        var emotionApiKey = System.Environment.GetEnvironmentVariable("EMOTION_API_KEY");
                        EmotionServiceClient emotionServiceClient = new EmotionServiceClient(emotionApiKey);

                        try
                        {
                            Emotion[] emotionResult = await emotionServiceClient.RecognizeAsync(photoUrl);
                            var emotionScores = emotionResult[0].Scores;
                            var happinessScore = Math.Ceiling(emotionScores.Happiness * 100);
                            if (happinessScore > 70)
                            {
                                responseMsg = "笑顔指数 " + happinessScore + "% ですね！　お仕事楽しんで!!";
                            }
                            else
                            {
                                responseMsg = "笑顔指数 " + happinessScore + "% ですね...　少し休憩しませんか?";
                            }
                        }
                        catch (Exception e)
                        {
                            log.Error(e.ToString());
                            responseMsg = "判断できませんでした。違う写真を送ってください。";
                        }
                    }
                }
            }

            Activity reply = activity.CreateReply(responseMsg);
            await connector.Conversations.ReplyToActivityAsync(reply);
            break;
        case ActivityTypes.ConversationUpdate:
        case ActivityTypes.ContactRelationUpdate:
        case ActivityTypes.Typing:
        case ActivityTypes.DeleteUserData:
        case ActivityTypes.Ping:
        default:
            log.Error($"Unknown activity type ignored: {activity.GetActivityType()}");
            break;
    }
}