using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using System.Threading;

namespace JustBigO_Fun_.Services.AI
{
    public interface ICodeTranslatorAgent
    {
        IAsyncEnumerable<string> TranslateAndStreamAsync(string sourceCode, string sourceLang, string targetLang, CancellationToken cancellationToken = default);
    }

    public class SemanticKernelTranslator : ICodeTranslatorAgent
    {
        private readonly Kernel _kernel;

        public SemanticKernelTranslator(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async IAsyncEnumerable<string> TranslateAndStreamAsync(
            string sourceCode,
            string sourceLang,
            string targetLang,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prompt = @"You are an expert polyglot programmer. 
Translate the following code from {{ $sourceLang }} to {{ $targetLang }}.
Output ONLY the translated code. Do not include markdown formatting (like ```java), explanations, or notes.
Maintain the exact logic and method signatures.

Original {{ $sourceLang }} Code:
{{ $sourceCode }}";

            var arguments = new KernelArguments()
            {
                { "sourceCode", sourceCode },
                { "sourceLang", sourceLang },
                { "targetLang", targetLang }
            };

            // Stream the LLM response token by token
            var stream = _kernel.InvokePromptStreamingAsync(prompt, arguments, cancellationToken: cancellationToken);

            await foreach (var chunk in stream)
            {
                yield return chunk.ToString();
            }
        }
    }
}