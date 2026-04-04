using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using CrsterCommand.Models;

namespace CrsterCommand.Services;

public class AIService
{
    private readonly StorageService _storageService;

    public AIService(StorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string> RunMacroPromptAsync(string systemPrompt, string userInput, string? modelOverride = null)
    {
        var apiKey = _storageService.GetAiApiKey();
        var modelName = string.IsNullOrWhiteSpace(modelOverride)
            ? (_storageService.GetAiModel() ?? "gemini-2.5-flash")
            : modelOverride;

        if (string.IsNullOrWhiteSpace(apiKey))
            return "Please provide a valid Gemini API Key in the Settings page.";

        if (string.IsNullOrWhiteSpace(systemPrompt))
            return "System prompt is required.";

        if (string.IsNullOrWhiteSpace(userInput))
            return "User input is required.";

        try
        {
            var client = new Client(apiKey: apiKey);
            var composedPrompt =
                $"System instruction:\n{systemPrompt}\n\n" +
                $"User input:\n{userInput}\n\n" +
                "Return only the direct answer unless the user asks for explanation.";

            var response = await client.Models.GenerateContentAsync(
                model: modelName,
                contents: new List<Content>
                {
                    new Content
                    {
                        Role = "user",
                        Parts = new List<Part> { new Part { Text = composedPrompt } }
                    }
                }
            );

            return response.Text?.Trim() ?? "No response available.";
        }
        catch (Exception ex)
        {
            return "AI Error: " + ex.Message;
        }
    }

    public async Task<string> GenerateNoteDescriptionAsync(BaseNoteItem item, Stream? fileStream = null)
    {
        var apiKey = _storageService.GetAiApiKey();
        var modelName = _storageService.GetAiModel() ?? "gemini-2.5-flash";

        if (string.IsNullOrEmpty(apiKey))
            return "AI Key Missing - No Description Generated.";

        try
        {
            var client = new Client(apiKey: apiKey);
            var prompt = "";
            var parts = new List<Part>();

            if (item is MemoryNote memory)
            {
                prompt = "Analyze the following memory/note content.\n" +
                         "1. Identify the content type (e.g., URL, Comment, Source Code, SQL, regular text).\n" +
                         "2. Provide a concise description of the purpose or content.\n" +
                         "Constraint: Maximum 30 words total.\n\n" +
                         $"CONTENT:\n{memory.Content}";
                parts.Add(new Part { Text = prompt });
            }
            else if (item is TodoItem todo)
            {
                var tasks = string.Join(", ", todo.Tasks.Select(t => t.Todo));
                prompt = "Analyze this list of tasks.\n" +
                         "1. Determine the category (e.g., Grocery, Work, Wish List, Daily tasks, OKR).\n" +
                         "2. Briefly summarize the objective.\n" +
                         "Constraint: Maximum 10 words total.\n\n" +
                         $"TASKS: {tasks}";
                parts.Add(new Part { Text = prompt });
            }
            else if (item is FileItem file && fileStream != null)
            {
                var mimeType = GetMimeType(file.FileType);
                
                if (fileStream.Length < 2 * 1024 * 1024 && mimeType != "application/octet-stream") // < 2MB and supported
                {
                    prompt = "Analyze the provided file content and provide the following details:\n" +
                             "1. File Classification: Identify the content type.\n" +
                             "2. Brief Summary: Provide a maximum 20-word summary of the content.";
                    parts.Add(new Part { Text = prompt });
                    
                    byte[] buffer = new byte[fileStream.Length];
                    fileStream.Position = 0;
                    int totalRead = 0;
                    while (totalRead < buffer.Length)
                    {
                        int read = await fileStream.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }
                    
                    parts.Add(new Part 
                    { 
                        InlineData = new Blob 
                        { 
                            MimeType = mimeType, 
                            Data = buffer 
                        } 
                    });
                }
                else // > 2MB or unsupported binary format
                {
                    fileStream.Position = 0;
                    string md5 = CalculateHash(fileStream, MD5.Create());
                    fileStream.Position = 0;
                    string sha256 = CalculateHash(fileStream, SHA256.Create());
                    
                    var info = $"Filename: {file.FileName}\nSize: {fileStream.Length} bytes\nExtension: {file.FileType}\nMD5: {md5}\nSHA256: {sha256}";
                    prompt = "Analyze the following file metadata and provide these details:\n" +
                             "1. File Classification: Identify based on filename and extension.\n" +
                             "2. Brief Summary: Provide a maximum 20-word summary based on the available metadata.\n\n" +
                             $"FILE INFO:\n{info}";
                    parts.Add(new Part { Text = prompt });
                }
            }
            else if (item is VaultItem)
            {
                return item.Description ?? "Private information";
            }
            else
            {
                return "";
            }

            var response = await client.Models.GenerateContentAsync(
                model: modelName,
                contents: new List<Content> { new Content { Role = "user", Parts = parts } }
            );

            return response.Text?.Trim() ?? "No description available.";
        }
        catch (Exception ex)
        {
            return "AI Error: " + ex.Message;
        }
    }

    public async Task<string> ExplainImageAsync(byte[] imageBytes)
    {
        var apiKey = _storageService.GetAiApiKey();
        var modelName = _storageService.GetAiModel() ?? "gemini-2.5-flash";

        if (string.IsNullOrEmpty(apiKey))
            return "Please provide a valid Gemini API Key in the Settings page.";

        try
        {
            var client = new Client(apiKey: apiKey);

            var prompt = "Task: Classify and extract data from the image.\n" +
                         "Categories:\n" +
                         "- FORM: Detected labels and input fields. Map them as a JSON object {label: value}.\n" +
                         "- TEXT: Primarily textual document. Extract the main blocks of text.\n" +
                         "- IMAGE: A visual drawing, photo, or art. Provide a professional description/prompt.\n\n" +
                         "Instructions:\n" +
                         "1. Use FORM category if any structured fields are detected.\n" +
                         "2. Return RAW JSON ONLY.\n" +
                         "3. DO NOT include markdown code blocks (e.g., no ```json).\n\n" +
                         "Output Format: { \"type\": \"FORM\"|\"TEXT\"|\"IMAGE\", \"result\": string|object|null }";

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

    private string CalculateHash(Stream stream, HashAlgorithm algorithm)
    {
        byte[] hash = algorithm.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private string GetMimeType(string extension)
    {
        extension = extension.ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".markdown" => "text/markdown",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".csv" => "text/csv",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
