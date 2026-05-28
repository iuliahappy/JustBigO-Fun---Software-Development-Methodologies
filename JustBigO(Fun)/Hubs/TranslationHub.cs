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

            // CRITICAL FIX: Use the Docker Reflexion loop instead of the old stream!
            var finalValidatedCode = await _translatorAgent.TranslateWithReflexionAsync(sourceCode, sourceLang, targetLang);

            // Send the final, validated code to the right-side editor
            await Clients.Caller.SendAsync("ReceiveCodeChunk", finalValidatedCode);
        }
    }
}