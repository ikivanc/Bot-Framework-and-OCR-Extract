// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BotOCRExtract.Helpers;
using BotOCRExtract.Model;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BotOCRExtract
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class BotOCRExtractBot : IBot
    {
        private readonly BotOCRExtractAccessors _accessors;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="conversationState">The managed conversation state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public BotOCRExtractBot(ConversationState conversationState, ILoggerFactory loggerFactory)
        {
            if (conversationState == null)
            {
                throw new System.ArgumentNullException(nameof(conversationState));
            }

            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<BotOCRExtractBot>();
            _logger.LogTrace("Turn start.");
        }



        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {

            if (turnContext.Activity.Type == ActivityTypes.Message)
            {

               

                ConnectorClient connector = new ConnectorClient(new Uri(turnContext.Activity.ServiceUrl));

                var activity = turnContext.Activity;
                var reply = activity.CreateReply();

                var imageAttachment = turnContext.Activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
                if (imageAttachment != null)
                {
                    Task.Run(async () =>
                    {
                        var ocrresult = await this.GetCaptionAsync(turnContext.Activity, connector);
                        var ocroutputtext = String.Join(" ", ocrresult.ToArray());

                        try
                        {
                            // Create Adaptive Card for confirmation view
                            var cardAttachment = await this.CreateAdaptiveCardUserInfo(@"Dialogs\Main\Resources\ConfirmMail.json", turnContext, ocrresult);
                            reply = turnContext.Activity.CreateReply();
                            reply.Attachments = new List<Attachment>() { cardAttachment };
                            await turnContext.SendActivityAsync(reply, cancellationToken).ConfigureAwait(false); ;
                        }
                        catch
                        {
                            await turnContext.SendActivityAsync("An error occured during OCR parsing", null, null, cancellationToken);
                        }

                    }).Wait();
                }
                else if (string.IsNullOrEmpty(turnContext.Activity.Text))
                {
                    dynamic value = turnContext.Activity.Value;
                    string text = value["adaptiveResponse"];  // The property will be named after your text input's ID
                    text = string.IsNullOrEmpty(text) ? "." : text; // In case the text input is empty
                    reply = turnContext.Activity.CreateReply();
                    reply.Text = text;
                    await turnContext.SendActivityAsync(reply, cancellationToken).ConfigureAwait(false); ;
                }
                else
                {

                }
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                // Check for user first visit
                if (turnContext.Activity.MembersAdded != null)
                {
                    foreach (var member in turnContext.Activity.MembersAdded)
                    {
                        if (member.Id != turnContext.Activity.Recipient.Id)
                        {
                            await turnContext.SendActivityAsync("Welcome to Form Extract Bot. I'm here to help you!");
                            await SendSuggestedActionsAsync(turnContext, cancellationToken);
                        }
                    }
                }
            }
        }


        private static async Task SendSuggestedActionsAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var reply = turnContext.Activity.CreateReply("Please upload your form I'll extract data for you, If you would like to apply manually click below");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction() { Title = "Apply Manually", Type = ActionTypes.ImBack, Value = "Apply Manually" },
                },
            };
            await turnContext.SendActivityAsync(reply, cancellationToken);
        }

        private static List<string> ExtractKeyValuePairs(Line[] lines)
        {
            //Initialize settings
            List<TextExtract> searchKeyList = Helper.RetrieveAllSearchTextKeyFields();
            List<Word> textvalues = new List<Word>();
            List<string> result = new List<string>();
            int b_margin, a_margin, b_width, a_height;

            // Extract regions of text words
            foreach (Line sline in lines)
            {
                foreach (Word sword in sline.Words)
                {
                    int[] wvalues = sword.BoundingBox;
                    textvalues.Add(new Word { Text = sword.Text, BoundingBox = wvalues });
                }
            }

            // Search Key-Value Pairs inside the documents
            if (searchKeyList.Count > 0)
            {
                foreach (TextExtract key in searchKeyList)
                {
                    var resultkeys = textvalues.Where(a => a.Text.Contains(key.Text));
                    foreach (var tv in resultkeys)
                    {
                        // Assign all fields values per text
                        b_margin = key.MarginX;
                        a_margin = key.MarginY;
                        b_width = key.Width;
                        a_height = key.Height;


                        // For height It's looking for 10px above
                        string txtreply = string.Join(" ",
                                                    from a in textvalues
                                                    where 
                                                    (a.BoundingBox[0] >= tv.BoundingBox[0] + b_margin) && 
                                                    (a.BoundingBox[0] <= tv.BoundingBox[0] + b_margin + b_width) && 
                                                    (a.BoundingBox[1] >= tv.BoundingBox[1] - a_margin) && 
                                                    (a.BoundingBox[1] <= tv.BoundingBox[1] + a_height)
                                                    select (string)a.Text);
                        result.Add(tv.Text + " - " + txtreply);
                    }
                }

                return result;
            }
            else
            {
                return null;
            }
        }

        private async Task<List<string>> GetCaptionAsync(Activity activity, ConnectorClient connector)
        {
            var imageAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
            if (imageAttachment != null)
            {

                var localFileName = imageAttachment.ContentUrl.Replace("[object Promise]", activity.ServiceUrl);
                

                using (var stream = await GetImageStream(connector, localFileName))
                {
                    return await MakeAnalysisWithImage(stream);
                }
            }

            // If we reach here then the activity is neither an image attachment nor an image URL.
            throw new ArgumentException("The activity doesn't contain a valid image attachment or an image URL.");
        }

        const string subscriptionKey = "KEY";
        const string uriBase = "https://northeurope.api.cognitive.microsoft.com/vision/v2.0/recognizeText";

        static async Task<List<string>> MakeAnalysisWithImage(Stream stream)
        {
            List<string> result = new List<string>();
            HttpClient client = new HttpClient();

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            string requestParameters = String.Format("?mode=Printed");

            // Assemble the URI for the REST API method.
            string uri = uriBase + requestParameters;

            HttpResponseMessage response;

            BinaryReader binaryReader = new BinaryReader(stream);
            byte[] byteData = binaryReader.ReadBytes((int)stream.Length);

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Asynchronously call the REST API method.
                response = await client.PostAsync(uri, content);
            }

            string operationLocation = null;

            // The response contains the URI to retrieve the result of the process.
            if (response.IsSuccessStatusCode)
            {
                operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
            }

            string contentString;
            int i = 0;
            do
            {
                System.Threading.Thread.Sleep(1000);
                response = await client.GetAsync(operationLocation);
                contentString = await response.Content.ReadAsStringAsync();
                ++i;
            }
            while (i < 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1);


            string json = response.Content.ReadAsStringAsync().Result;
            json = json.TrimStart(new char[] { '[' }).TrimEnd(new char[] { ']' });
            RecognizeText ocrOutput = JsonConvert.DeserializeObject<RecognizeText>(json);

            if (ocrOutput != null && ocrOutput.RecognitionResult != null && ocrOutput.RecognitionResult.Lines != null)
            {

                List<string> resultText = new List<string>();

                resultText = (from Line sline in ocrOutput.RecognitionResult.Lines
                              select (string)sline.Text).ToList<string>();

                resultText = ExtractKeyValuePairs(ocrOutput.RecognitionResult.Lines);

                return resultText;
            }
            else
            {
                return null;
            }
        }

        private static async Task<Stream> GetImageStream(ConnectorClient connector, string imageUrl)
        {
            using (HttpClient client = new HttpClient())
            {
              
                var uri = new Uri(imageUrl);
                if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                    return await client.GetStreamAsync(uri);
                }
                else
                {
                    var response = await client.GetAsync(imageUrl);
                    if (!response.IsSuccessStatusCode) return null;
                    return await response.Content?.ReadAsStreamAsync();
                }
            }
        }

        private static async Task<string> GetTokenAsync(ConnectorClient connector)
        {
            var credentials = connector.Credentials as MicrosoftAppCredentials;
            if (credentials != null)
            {
                return await credentials.GetTokenAsync();
            }

            return null;
        }

        // Generate adaptive card for UserInfo
        private async Task<Attachment> CreateAdaptiveCardUserInfo(string filePath, ITurnContext turnContext, List<string> keyValuePairs)
        {
            var adaptiveCardJson = File.ReadAllText(filePath);

            foreach (var str in keyValuePairs)
            {
                string strKey = "#" + str.Split(" - ")[0].Replace(",", "").Replace(":","");  //Key Values should be cleaned
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


    }
}
