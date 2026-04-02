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
                prompt = $"Explain the usage of this concept/memory in 50 words max: {memory.Content}";
                parts.Add(new Part { Text = prompt });
            }
            else if (item is TodoItem todo)
            {
                var tasks = string.Join(", ", todo.Tasks.Select(t => t.Todo));
                prompt = $"Explain what this todo list is about in 50 words max: {tasks}";
                parts.Add(new Part { Text = prompt });
            }
            else if (item is FileItem file && fileStream != null)
            {
                if (fileStream.Length < 2 * 1024 * 1024) // < 2MB
                {
                    prompt = "Describe what this file contains in 50 words max.";
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
                            MimeType = GetMimeType(file.FileType), 
                            Data = buffer 
                        } 
                    });
                }
                else // > 2MB
                {
                    fileStream.Position = 0;
                    string md5 = CalculateHash(fileStream, MD5.Create());
                    fileStream.Position = 0;
                    string sha256 = CalculateHash(fileStream, SHA256.Create());
                    
                    var info = $"Filename: {file.FileName}\nSize: {fileStream.Length} bytes\nExtension: {file.FileType}\nMD5: {md5}\nSHA256: {sha256}";
                    prompt = $"Based on this file info, tell me:\n1. What file it is?\n2. Is it potentially dangerous?\n3. What application to open it?\n4. Explain file in 50 words max.\n\nINFO:\n{info}";
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
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".csv" => "text/csv",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }
}
