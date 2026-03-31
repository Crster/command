using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;

namespace AvaloniaRobotApp.Services;

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
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GEMINI_API_KEY")
            return "Please provide a valid Gemini API Key in the Settings page.";

        try
        {
            var googleAI = new GoogleAI(apiKey);
            // v2.5-flash-lite as requested
            var model = googleAI.GenerativeModel("gemini-2.5-flash-lite");
            
            var request = new GenerateContentRequest
            {
                Contents = new List<Content>
                {
                    new Content
                    {
                        Role = "user",
                        Parts = new List<IPart>
                        {
                            new Part { Text = "Classify: FORM, TEXT, IMAGE.\n" +
                                             "1. FORM: If labels + inputs/boxes detected (mandatory priority). Map {label: value}.\n" +
                                             "2. TEXT: Only for pure text/doc blocks. Read all.\n" +
                                             "3. IMAGE: If ball/person/art. Provide generation prompt.\n" +
                                             "Output RAW JSON ONLY: { \"type\": \"FORM\"|\"TEXT\"|\"IMAGE\", \"result\": string|object|null }.\n" +
                                             "No markdown code blocks." },
                            new Part { InlineData = new InlineData { MimeType = "image/png", Data = Convert.ToBase64String(imageBytes) } }
                        }
                    }
                }
            };

            var response = await model.GenerateContent(request);
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
