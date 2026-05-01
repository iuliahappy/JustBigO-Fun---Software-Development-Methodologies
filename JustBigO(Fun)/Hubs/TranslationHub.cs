using Microsoft.AspNetCore.SignalR;
using JustBigO_Fun_.Services.AI;
using System.Threading.Tasks;

namespace JustBigO_Fun_.Hubs
{
    public class TranslationHub : Hub
    {
        private readonly ICodeTranslatorAgent _translatorAgent;

        public TranslationHub(ICodeTranslatorAgent translatorAgent)
        {
            _translatorAgent = translatorAgent;
        }

        public async Task TranslateCode(string sourceCode, string sourceLang, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(sourceCode)) return;

            var tokenStream = _translatorAgent.TranslateAndStreamAsync(sourceCode, sourceLang, targetLang);

            await foreach (var token in tokenStream)
            {
                // Push each chunk of code to the specific user who asked for it
                await Clients.Caller.SendAsync("ReceiveCodeChunk", token);
            }
        }
    }
}