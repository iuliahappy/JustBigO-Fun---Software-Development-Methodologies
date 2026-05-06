using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace JustBigO_Fun_.Services.AI
{
    public interface ICodeTranslatorAgent
    {
        IAsyncEnumerable<string> TranslateAndStreamAsync(string sourceCode, string sourceLang, string targetLang, CancellationToken cancellationToken = default);
        Task<string> TranslateWithReflexionAsync(string sourceCode, string sourceLang, string targetLang, CancellationToken cancellationToken = default);
    }

    public class SemanticKernelTranslator : ICodeTranslatorAgent
    {
        private readonly Kernel _kernel;
        private readonly ICodeExecutor _dockerExecutor;

        // FIX: Injected ICodeExecutor into the constructor
        public SemanticKernelTranslator(Kernel kernel, ICodeExecutor dockerExecutor)
        {
            _kernel = kernel;
            _dockerExecutor = dockerExecutor;
        }

        // METHOD 1: The original streaming method
        public async IAsyncEnumerable<string> TranslateAndStreamAsync(
            string sourceCode,
            string sourceLang,
            string targetLang,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prompt = @"<message role=""system"">You are a strict, machine-like code translation API.
You have no personality. You do not explain things. 

ABSOLUTE LAWS:
1. Output ONLY the translated code. 
2. DO NOT wrap the code in markdown blocks.
3. DO NOT solve the problem. DO NOT complete the logic.
4. If the input function is empty, the output function MUST be completely empty.
</message>

<message role=""user"">Translate the exact syntax of this {{ $sourceLang }} code into {{ $targetLang }}:
{{ $sourceCode }}</message>";

            var arguments = new KernelArguments()
            {
                { "sourceCode", sourceCode },
                { "sourceLang", sourceLang },
                { "targetLang", targetLang }
            };

            var stream = _kernel.InvokePromptStreamingAsync(prompt, arguments, cancellationToken: cancellationToken);

            await foreach (var chunk in stream)
            {
                yield return chunk.ToString();
            }
        }

        // METHOD 2: The new Reflexion method (Docker Loop)
        public async Task<string> TranslateWithReflexionAsync(
            string sourceCode,
            string sourceLang,
            string targetLang,
            CancellationToken cancellationToken = default)
        {
            var prompt = @"<message role=""system"">You are a strict, machine-like code translation API.
ABSOLUTE LAWS:
1. Output ONLY the translated code, even if it is incomplete, broken, or uses placeholder statements like 'pass'. 
2. DO NOT wrap the code in markdown blocks.
3. DO NOT solve the problem. DO NOT complete the logic.
4. If the input function is empty, uses 'pass', or contains no logic, the output function MUST be completely empty.
5. Use modern, idiomatic data structures (e.g., std::vector in C++).
</message>

<message role=""user"">Translate the exact syntax of this {{ $sourceLang }} code into {{ $targetLang }}:

{{ $sourceCode }}</message>";

            var arguments = new KernelArguments()
            {
                { "sourceCode", sourceCode },
                { "sourceLang", sourceLang },
                { "targetLang", targetLang }
            };

            int maxAttempts = 5;
            string currentPrompt = prompt;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var aiResponse = await _kernel.InvokePromptAsync(currentPrompt, arguments, cancellationToken: CancellationToken.None);

                string draftedCode = aiResponse.ToString()
                                    .Replace("```" + targetLang, "")
                                    .Replace("```", "")
                                    .Trim();

                // 3. Send to Docker Sandbox to compile/run IN MEMORY
                var result = await _dockerExecutor.TestRawCodeAsync(draftedCode, targetLang);

                // 4. Check the results
                if (result.IsSuccess)
                {
                    // It compiled perfectly! Return it to the UI.
                    return draftedCode;
                }
                else
                {
                    // 5. It failed! Append the Docker error to the prompt and loop again.
                    if (attempt == maxAttempts)
                    {
                        return $"// AI failed to generate compiling code after {maxAttempts} attempts.\n// Last Error:\n/* {result.ErrorMessage} */\n\n{draftedCode}";
                    }

                    currentPrompt += $@"
<message role=""assistant"">{draftedCode}</message>
<message role=""user"">Your code failed to compile. Here is the exact compiler error from Docker:

{result.ErrorMessage}

Fix the code. Remember the ABSOLUTE LAWS: Output ONLY the corrected raw code, no markdown, no explanations.</message>";
                }
                
            }
            return "// Error: Reflexion loop failed.";
        }
    }
}