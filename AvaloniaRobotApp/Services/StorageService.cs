using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;
using AvaloniaRobotApp.Models;

namespace AvaloniaRobotApp.Services;

public class StorageService : IDisposable
{
    private static string GetDefaultDbPath() => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AvaloniaRobotApp", "toolkit.db");

    private static string GetConfigPath() => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaRobotApp", "config.json");

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
        
        // Attempt to load from JSON config
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null) return settings;
            }
            catch { /* Ignore and return default */ }
        }

        // Migrate from old config.txt if it exists
        var oldConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaRobotApp", "config.txt");
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

            // Use Connection=Shared to allow multiple processes (or OneDrive) to access the file
            var connectionString = $"Filename={dbPath};Connection=shared";
            _db = new LiteDatabase(connectionString);
        }
        catch (IOException ex)
        {
            // If the file is strictly locked, we can't do much but maybe notify the user or 
            // fallback to a temporary in-memory DB or just let it bubble up with a better message.
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

    public ILiteCollection<TodoItem> GetTodos() => _db!.GetCollection<TodoItem>("todos");
    public ILiteCollection<Reminder> GetReminders() => _db!.GetCollection<Reminder>("reminders");
    public ILiteCollection<MemoryNote> GetMemoryNotes() => _db!.GetCollection<MemoryNote>("notes");
    public ILiteCollection<VaultItem> GetVaultItems() => _db!.GetCollection<VaultItem>("vault");
    
    public ILiteStorage<string> GetFileStorage() => _db!.FileStorage;

    public string GetCurrentDbPath() => _settings.DbPath;

    public void Dispose()
    {
        _db?.Dispose();
    }
}
