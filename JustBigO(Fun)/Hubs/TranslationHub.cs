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

            // CRITICAL: Call the new Reflexion method, not the streaming one!
            var finalValidatedCode = await _translatorAgent.TranslateWithReflexionAsync(sourceCode, sourceLang, targetLang);

            // Send the final, validated code to the frontend UI
            await Clients.Caller.SendAsync("ReceiveCodeChunk", finalValidatedCode);
        }
    }
}