// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using BotOCRExtract.Services;



namespace BotOCRExtract.Dialogs.Main
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
        private readonly ILogger _logger;
        private readonly string _subscriptionKey;
        private readonly string _uriEndPoint;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="conversationState">The managed conversation state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public BotOCRExtractBot(ConversationState conversationState, ILoggerFactory loggerFactory, Dictionary<string, string> ocrCridentials)
        {
            if (conversationState == null)
            {
                throw new System.ArgumentNullException(nameof(conversationState));
            }

            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            if (ocrCridentials != null)
            {
                foreach (var key in ocrCridentials.Keys)
                {
                    string uri = ocrCridentials[key];
                    _subscriptionKey = key ?? throw new ArgumentNullException(nameof(key));
                    _uriEndPoint = uri ?? throw new ArgumentNullException(nameof(uri));
                }
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
                        var ocrresult = await OCRHelper.GetCaptionAsync(turnContext.Activity, connector, _subscriptionKey, _uriEndPoint);
                        var ocroutputtext = String.Join(" ", ocrresult.ToArray());

                        try
                        {
                            // Create Adaptive Card for confirmation view
                            string jsonCardPath = @"Dialogs\Main\Resources\ConfirmMail.json";
                            var cardAttachment = await UIHelper.CreateAdaptiveCardUserInfo(jsonCardPath,turnContext, ocrresult);
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
                    // Check response comes from Adaptive Card Actions
                    dynamic value = turnContext.Activity.Value;
                    string text = value["adaptiveResponse"];  // The property will be named after your text input's ID
                    text = string.IsNullOrEmpty(text) ? "." : text; // In case the text input is empty
                    reply = turnContext.Activity.CreateReply();
                    reply.Text = text;
                    await turnContext.SendActivityAsync(reply, cancellationToken).ConfigureAwait(false); ;
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
                            await UIHelper.SendSuggestedActionsAsync(turnContext, cancellationToken);
                        }
                    }
                }
            }
        }

    }
}
