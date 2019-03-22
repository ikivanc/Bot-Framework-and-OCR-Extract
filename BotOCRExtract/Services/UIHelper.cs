using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace BotOCRExtract.Services
{
    public class UIHelper
    {
        // Generate adaptive card for UserInfo
        public static async Task<Attachment> CreateAdaptiveCardUserInfo(string filePath, ITurnContext turnContext, List<string> keyValuePairs)
        {
            try
            {
                var adaptiveCardJson = File.ReadAllText(filePath);

                // this code snippet generates items for JSON placeholder in adaptive card
                string strKey = "#GENERATEITEMS";
                List<string> strValueList = new List<string>();

                foreach (var str in keyValuePairs)
                {
                    strValueList.Add("{ \"title\": \"" + str.Split(" - ")[0] + ":\", \"value\": \"" + str.Split(" - ")[1] + "\" }");
                }

                string strValue = string.Join(",", strValueList);
                adaptiveCardJson = adaptiveCardJson.ToString().Replace(strKey, strValue);


                //This is for predefined keys & Values
                //foreach (var str in keyValuePairs)
                //{
                //    string strKey = "#" + str.Split(" - ")[0].Replace(",", "").Replace(":", "");  //Key Values should be cleaned
                //    string strValue = str.Split(" - ")[1];
                //    adaptiveCardJson = adaptiveCardJson.ToString().Replace(strKey, strValue);
                //}

                var adaptiveCardAttachment = new Attachment()
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = JsonConvert.DeserializeObject(adaptiveCardJson),
                };

                return adaptiveCardAttachment;
            }
            catch
            {
                return null;
            }
        }


        public static async Task SendSuggestedActionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var reply = turnContext.Activity.CreateReply("Please upload your form I'll extract data for you, If you would like to apply manually click below");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction() { Title = "Apply Manually", Type = ActionTypes.ImBack, Value = "Apply Manually" },
                    new CardAction() { Title = "Apply House Insurance", Type = ActionTypes.ImBack, Value = "Apply Manually" },
                    new CardAction() { Title = "Apply Car Insurance", Type = ActionTypes.ImBack, Value = "Apply Manually" },
                },
            };
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        //Show typing activity to User
        public static async Task SendTypingMessage(ITurnContext turnContext)
        {
            var typing = turnContext.Activity.CreateReply();
            typing.Type = ActivityTypes.Typing;
            await turnContext.SendActivityAsync(typing);
        }
    }
}
