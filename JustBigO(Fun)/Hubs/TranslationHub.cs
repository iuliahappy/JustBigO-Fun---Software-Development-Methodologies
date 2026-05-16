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

            // Increased to 60s to allow multiple reflexion loops (AI gen + Docker test)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            try
            {
                // CRITICAL: Call the new Reflexion method, not the streaming one!
                // We pass the cancellation token to handle the timeout
                var finalValidatedCode = await _translatorAgent.TranslateWithReflexionAsync(sourceCode, sourceLang, targetLang, cts.Token);

                // Send the final, validated code to the frontend UI
                await Clients.Caller.SendAsync("ReceiveCodeChunk", finalValidatedCode);
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.SendAsync("ReceiveCodeChunk", "\n// [TIMEOUT] The AI agent took too long to respond (10s limit).\n// Please try again or simplify the code.");
            }
            catch (System.Exception ex)
            {
                await Clients.Caller.SendAsync("ReceiveCodeChunk", $"\n// [ERROR] An unexpected error occurred: {ex.Message}");
            }
        }
    }
}