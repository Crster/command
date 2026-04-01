using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

namespace CrsterCommand.Services;

public class AIService
{
    private readonly StorageService _storageService;

    public AIService(StorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string> ExplainImageAsync(byte[] imageBytes)
    {
        var apiKey = _storageService.GetAiApiKey();
        var modelName = _storageService.GetAiModel() ?? "gemini-1.5-flash";

        if (string.IsNullOrEmpty(apiKey))
            return "Please provide a valid Gemini API Key in the Settings page.";

        try
        {
            var client = new Client(apiKey: apiKey);

            var prompt = "Classify: FORM, TEXT, IMAGE.\n" +
                         "1. FORM: If labels + inputs/boxes detected (mandatory priority). Map {label: value}.\n" +
                         "2. TEXT: Only for pure text/doc blocks. Read all.\n" +
                         "3. IMAGE: If ball/person/art. Provide generation prompt.\n" +
                         "Output RAW JSON ONLY: { \"type\": \"FORM\"|\"TEXT\"|\"IMAGE\", \"result\": string|object|null }.\n" +
                         "No markdown code blocks.";

            var response = await client.Models.GenerateContentAsync(
                model: modelName,
                contents: new List<Content>
                {
                    new Content
                    {
                        Role = "user",
                        Parts = new List<Part>
                        {
                            new Part { Text = prompt },
                            new Part 
                            { 
                                InlineData = new Blob 
                                { 
                                    MimeType = "image/png", 
                                    Data = imageBytes 
                                } 
                            }
                        }
                    }
                }
            );

            var json = response.Text?.Trim() ?? "{\"type\": \"UNKNOWN\", \"result\": null}";
            
            // Clean up possible markdown backticks
            if (json.StartsWith("```json")) json = json.Substring(7).Trim();
            if (json.StartsWith("```")) json = json.Substring(3).Trim();
            if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3).Trim();
            
            return json;
        }
        catch (Exception ex)
        {
            return "AI Error: " + ex.Message;
        }
    }
}
