using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using VidexBot.CognitiveModels;

namespace VidexBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly VidexIntentRecognizer _luisRecognizer;
        private readonly VidexClient _videxClient;
        private readonly IConfiguration _configuration;
        protected readonly ILogger Logger;

        public MainDialog(IConfiguration configuration, VidexIntentRecognizer luisRecognizer, VidexClient videxClient, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _configuration = configuration;
            _luisRecognizer = luisRecognizer;
            _videxClient = videxClient;
            Logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Context.Activity.Text != null)
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
            else
            {
                // Use the text provided in FinalStepAsync or the default if it is the first time.
                var messageText = stepContext.Options?.ToString() ?? "What do you want to do, open or close the gates, or latch them?";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var luisResult = await _luisRecognizer.RecognizeAsync<VidexLUIS>(stepContext.Context, cancellationToken);
            switch (luisResult.TopIntent().intent)
            {
                case VidexLUIS.Intent.CheckBalance:
                    {
                        var msgText = "Checking balance";
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text(msgText, msgText, InputHints.IgnoringInput), cancellationToken);
                        await _videxClient.CheckBalance();
                        break;
                    }

                case VidexLUIS.Intent.OpenGates:
                case VidexLUIS.Intent.LatchGates:
                    {
                        stepContext.Values["Intent"] = luisResult.TopIntent().intent;
                        var messageText = "What is the magic word?";
                        var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                        return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
                    }
                case VidexLUIS.Intent.CloseGates:
                    {
                        var msgText = "Closing gates";
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text(msgText, msgText, InputHints.IgnoringInput), cancellationToken);
                        await _videxClient.CloseGates();
                        break;
                    }
                default:
                    var didntUnderstandMessageText = $"I didn't get that, I have no idea what you want from me.";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }
            return await stepContext.EndDialogAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Context.Activity.Text.Equals(_configuration["Passphrase"], System.StringComparison.InvariantCultureIgnoreCase))
            {
                switch ((VidexLUIS.Intent)stepContext.Values["Intent"])
                {
                    case VidexLUIS.Intent.LatchGates:
                        {
                            await _videxClient.LatchGates();
                            var msgText = "Latching gates open";
                            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msgText, msgText, InputHints.IgnoringInput), cancellationToken);
                            break;
                        }
                    case VidexLUIS.Intent.OpenGates:
                        {
                            await _videxClient.OpenGates();
                            var msgText = "Opening gates";
                            await stepContext.Context.SendActivityAsync(MessageFactory.Text(msgText, msgText, InputHints.IgnoringInput), cancellationToken);
                            break;
                        }
                    default:
                        break;
                }
            }
            else
            {
                var msgText = $"Nope, {stepContext.Context.Activity.Text} is not the magic word.  I'm not opening the gates.";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(msgText, msgText, InputHints.IgnoringInput), cancellationToken);
            }

            return await stepContext.EndDialogAsync();
        }
    }
}
