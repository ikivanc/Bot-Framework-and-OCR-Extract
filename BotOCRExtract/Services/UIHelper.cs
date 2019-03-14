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
            var adaptiveCardJson = File.ReadAllText(filePath);

            foreach (var str in keyValuePairs)
            {
                string strKey = "#" + str.Split(" - ")[0].Replace(",", "").Replace(":", "");  //Key Values should be cleaned
                string strValue = str.Split(" - ")[1];
                adaptiveCardJson = adaptiveCardJson.ToString().Replace(strKey, strValue);
            }

            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCardJson),
            };

            return adaptiveCardAttachment;
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
    }
}
