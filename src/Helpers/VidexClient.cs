using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace VidexBot
{
    /// <summary>
    /// VidexClient uses SMS messages to control the GSM series of audio intercoms
    /// Sending different codes and the corresponding pin code to the sim card number in the intercom will fire different
    /// events within the intercom's configuration.  Currently on the main events are handled:
    /// Open gates - opens relay #1
    /// Close gates (also unlatch gates) - closes relay #1
    /// Latch gates - latches relay #1
    /// Check balance - checks remaining balance on the sim card
    /// </summary>
    public class VidexClient
    {
        private readonly string _videxPinCode;
        private readonly string _twilioSID;
        private readonly string _twilioAuthToken;
        private readonly string _twilioSMSFrom;
        private readonly string _twilioSMSTo;

        private string OpenGatesSMS => $"{_videxPinCode}{VidexRequestCodes.TriggerRelay}";
        private string CloseGatesSMS => $"{_videxPinCode}{VidexRequestCodes.UnlatchRelay}";
        private string LatchGatesSMS => $"{_videxPinCode}{VidexRequestCodes.LatchRelay}";
        private string CheckBalanceSMS => $"{_videxPinCode}{VidexRequestCodes.CreditBalance}";

        public VidexClient(IConfiguration configuration)
        {
            _videxPinCode = configuration["VidexPin"];
            _twilioSID = configuration["TwilioSID"];
            _twilioAuthToken = configuration["TwilioAuthToken"];
            _twilioSMSFrom = configuration["TwilioSMSNumber"];
            _twilioSMSTo = configuration["VidexSMSNumber"];

            TwilioClient.Init(_twilioSID, _twilioAuthToken);
        }

        public async Task<MessageResource> OpenGates()
        {
            return await SendSMS(OpenGatesSMS);
        }

        public async Task<MessageResource> LatchGates()
        {
            return await SendSMS(LatchGatesSMS);
        }

        public async Task<MessageResource> CloseGates()
        {
            return await SendSMS(CloseGatesSMS);
        }

        public async Task<MessageResource> CheckBalance()
        {
            return await SendSMS(CheckBalanceSMS);
        }

        private async Task<MessageResource> SendSMS(string message)
        {
            var mr = await MessageResource.CreateAsync(
                body: message,
                from: new Twilio.Types.PhoneNumber(_twilioSMSFrom),
                to: new Twilio.Types.PhoneNumber(_twilioSMSTo)
            );

            return mr;
        }
    }

    public static class VidexRequestCodes
    {
        public const string TriggerRelay = "RLY";
        public const string LatchRelay = "RLA";
        public const string UnlatchRelay = "RUL";

        public const string GSMSignalStrength = "SIG?";

        public const string SoftwareVersion = "VER?";

        public const string CheckDate = @"CLK?""yy/mm/dd,hh:mm""";

        public const string CreditBalance = "BAL?";
    }
}
