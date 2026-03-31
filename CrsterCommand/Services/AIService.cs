using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using OpenAI;
using OllamaSharp;
using System.ClientModel;
using System.Linq;

namespace CrsterCommand.Services;

public class AIService
{
    private readonly StorageService _storageService;

    public AIService(StorageService storageService)
    {
        _storageService = storageService;
    }

    private IChatClient GetChatClient()
    {
        var provider = _storageService.GetAiServiceProvider() ?? "Gemini";
        var model = _storageService.GetAiModel() ?? "gemini-2.5-flash-lite";
        var apiKey = _storageService.GetAiApiKey() ?? "";
        var endpoint = _storageService.GetAiEndPoint();

        switch (provider)
        {
            case "Gemini":
                // Gemini has an OpenAI-compatible endpoint
                return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions 
                { 
                    Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") 
                }).AsChatClient(model);

            case "OpenAI":
                if (!string.IsNullOrEmpty(endpoint))
                {
                    return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions 
                    { 
                        Endpoint = new Uri(endpoint) 
                    }).AsChatClient(model);
                }
                return new OpenAIClient(new ApiKeyCredential(apiKey)).AsChatClient(model);

            case "Ollama":
                var ollamaUri = new Uri(string.IsNullOrEmpty(endpoint) ? "http://localhost:11434" : endpoint);
                return new OllamaApiClient(ollamaUri, model);

            case "HuggingFace":
                if (!string.IsNullOrEmpty(endpoint))
                {
                    return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions 
                    { 
                        Endpoint = new Uri(endpoint) 
                    }).AsChatClient(model);
                }
                throw new Exception("HuggingFace requires an endpoint configured.");

            default:
                throw new Exception($"Unsupported AI provider: {provider}");
        }
    }

    public async Task<string> ExplainImageAsync(byte[] imageBytes)
    {
        try
        {
            var client = GetChatClient();
            
            var prompt = "Classify: FORM, TEXT, IMAGE.\n" +
                         "1. FORM: If labels + inputs/boxes detected (mandatory priority). Map {label: value}.\n" +
                         "2. TEXT: Only for pure text/doc blocks. Read all.\n" +
                         "3. IMAGE: If ball/person/art. Provide generation prompt.\n" +
                         "Output RAW JSON ONLY: { \"type\": \"FORM\"|\"TEXT\"|\"IMAGE\", \"result\": string|object|null }.\n" +
                         "No markdown code blocks.";

            var message = new ChatMessage(ChatRole.User, prompt);
            message.Contents.Add(new DataContent(imageBytes, "image/png"));

            var response = await client.GetResponseAsync([message]);
            
            // Try to find the message in the response
            var responseText = response.Messages.FirstOrDefault()?.Text?.Trim() ?? "{\"type\": \"UNKNOWN\", \"result\": null}";
            
            // Clean up possible markdown backticks
            if (responseText.StartsWith("```json")) responseText = responseText.Substring(7).Trim();
            if (responseText.StartsWith("```")) responseText = responseText.Substring(3).Trim();
            if (responseText.EndsWith("```")) responseText = responseText.Substring(0, responseText.Length - 3).Trim();
            
            return responseText;
        }
        catch (Exception ex)
        {
            return "AI Error: " + ex.Message;
        }
    }
}
