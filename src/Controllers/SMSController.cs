using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Twilio.AspNet.Core;
using Twilio.TwiML;

namespace VidexBot.Controllers
{
    [Route("api/sms")]
    [ApiController]
    public class SMSController : TwilioController
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly ConcurrentDictionary<string, ConversationReference> _conversationReferences;
        private readonly string _appId;

        public SMSController(IBotFrameworkHttpAdapter adapter, IConfiguration configuration, ConcurrentDictionary<string, ConversationReference> conversationReferences)
        {
            _adapter = adapter;
            _appId = configuration["MicrosoftAppId"];
            _conversationReferences = conversationReferences;
        }

        /// <summary>
        /// This is the webhook that Twilio will fire when a message is received.  Currently this only handles a response from a balance request,
        /// but could easily be extended to handle other responses enabled by the Videx GSM series.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> PostAsync()
        {
            var form = await Request.ReadFormAsync();
            if (form != null)
            {
                // Ugly formatted response comes back from Vodafone - likely to be different format for other GSM providers
                // BAL=Yourbalanceis#2.98.Tocheckanyremainingallowancespleasecall1345,forfree.Thankyou OK VIDEX GSM
                string balanceText = form["Body"];
                if (balanceText != null)
                {
                    Regex regex = new Regex(@"([^#]\d*[.]+\d{0,2})");
                    var regexAmount = regex.Match(balanceText);

                    if (regexAmount.Success)
                    {
                        decimal amount = Convert.ToDecimal(regexAmount.Value);
                        string responseText = $"Your current balance is £{amount}";

                        Activity videxResponse = new Activity() { Type = "message", InputHint = "acceptingInput", Text = responseText };

                        if (!_conversationReferences.IsEmpty)
                        {
                            foreach (var conversationReference in _conversationReferences.Values)
                            {
                                await ((BotAdapter)_adapter).ContinueConversationAsync(
                                    _appId,
                                    conversationReference,
                                    (ITurnContext turnContext, CancellationToken cancellationToken) =>
                                    {
                                        MicrosoftAppCredentials.TrustServiceUrl(conversationReference.ServiceUrl);
                                        return turnContext.SendActivityAsync(videxResponse);
                                    }, default);
                            }
                        }
                    }
                }
            }

            var mr = new MessagingResponse();
            Response.ContentType = "text/xml";
            Response.StatusCode = StatusCodes.Status200OK;
            return TwiML(mr);
        }
    }
}
