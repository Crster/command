using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;
using CrsterCommand.Models;

namespace CrsterCommand.Services;

public class StorageService : IDisposable
{
    private static string GetDefaultDbPath() => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrsterCommand", "toolkit.db");

    private static string GetConfigPath() => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrsterCommand", "config.json");

    private UserSettings _settings;
    private LiteDatabase? _db;

    public StorageService()
    {
        _settings = LoadSettings();
        InitializeDatabase();
    }

    private UserSettings LoadSettings()
    {
        var configPath = GetConfigPath();
        
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null) return settings;
            }
            catch { }
        }

        var oldConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrsterCommand", "config.txt");
        var dbPath = GetDefaultDbPath();
        if (File.Exists(oldConfigPath))
        {
            dbPath = File.ReadAllText(oldConfigPath).Trim();
        }

        var newSettings = new UserSettings { DbPath = dbPath };
        SaveSettings(newSettings);
        return newSettings;
    }

    private void SaveSettings(UserSettings settings)
    {
        var configPath = GetConfigPath();
        var directory = Path.GetDirectoryName(configPath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
            
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }

    private void InitializeDatabase()
    {
        try
        {
            var dbPath = _settings.DbPath;
            var directory = Path.GetDirectoryName(dbPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var connectionString = $"Filename={dbPath};Connection=shared";
            _db = new LiteDatabase(connectionString);
        }
        catch (IOException ex)
        {
            throw new Exception($"Could not access database at {_settings.DbPath}. Please ensure it is not open in another program.", ex);
        }
    }

    public void ChangeDatabasePath(string newPath)
    {
        _db?.Dispose();
        _settings.DbPath = newPath;
        SaveSettings(_settings);
        InitializeDatabase();
    }

    public string? GetAiApiKey() => _settings.AiApiKey;
    public void SetAiApiKey(string? apiKey)
    {
        _settings.AiApiKey = apiKey;
        SaveSettings(_settings);
    }

    public string? GetAiModel() => _settings.AiModel;
    public void SetAiModel(string? model)
    {
        _settings.AiModel = model;
        SaveSettings(_settings);
    }

    public string? GetVaultPassword() => _settings.VaultPassword;
    public void SetVaultPassword(string? password)
    {
        _settings.VaultPassword = password;
        SaveSettings(_settings);
    }

    public ILiteCollection<TodoItem> GetTodos() => _db!.GetCollection<TodoItem>("todos");
    public ILiteCollection<Reminder> GetReminders() => _db!.GetCollection<Reminder>("reminders");
    public ILiteCollection<MemoryNote> GetMemoryNotes() => _db!.GetCollection<MemoryNote>("notes");
    public ILiteCollection<VaultItem> GetVaultItems() => _db!.GetCollection<VaultItem>("vault");
    public ILiteCollection<FileItem> GetFileItems() => _db!.GetCollection<FileItem>("files");
    public ILiteCollection<AiMacroApp> GetAiMacroApps() => _db!.GetCollection<AiMacroApp>("ai_macro_apps");
    
    public ILiteStorage<string> GetFileStorage() => _db!.FileStorage;

    public void UploadFile(string id, string fileName, Stream stream)
    {
        _db!.FileStorage.Upload(id, fileName, stream);
    }

    public bool DownloadFile(string id, Stream destination)
    {
        var file = _db!.FileStorage.FindById(id);
        if (file == null) return false;
        _db.FileStorage.Download(id, destination);
        return true;
    }

    public bool DeleteFile(string id)
    {
        return _db!.FileStorage.Delete(id);
    }

    public string GetCurrentDbPath() => _settings.DbPath;

    public void Dispose()
    {
        _db?.Dispose();
    }
}
