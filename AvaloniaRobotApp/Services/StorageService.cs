using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using AvaloniaRobotApp.Models;

namespace AvaloniaRobotApp.Services;

public class StorageService : IDisposable
{
    private static string GetDefaultDbPath() => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AvaloniaRobotApp", "toolkit.db");

    private string _dbPath;
    private LiteDatabase? _db;

    public StorageService()
    {
        // Load custom path from a simple config file in AppData if it exists
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaRobotApp", "config.txt");
        if (File.Exists(configPath))
        {
            _dbPath = File.ReadAllText(configPath).Trim();
        }
        else
        {
            _dbPath = GetDefaultDbPath();
        }

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            var directory = Path.GetDirectoryName(_dbPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Use Connection=Shared to allow multiple processes (or OneDrive) to access the file
            var connectionString = $"Filename={_dbPath};Connection=shared";
            _db = new LiteDatabase(connectionString);
        }
        catch (IOException ex)
        {
            // If the file is strictly locked, we can't do much but maybe notify the user or 
            // fallback to a temporary in-memory DB or just let it bubble up with a better message.
            throw new Exception($"Could not access database at {_dbPath}. Please ensure it is not open in another program.", ex);
        }
    }

    public void ChangeDatabasePath(string newPath)
    {
        _db?.Dispose();
        _dbPath = newPath;
        
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaloniaRobotApp", "config.txt");
        var directory = Path.GetDirectoryName(configPath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
            
        File.WriteAllText(configPath, newPath);
        InitializeDatabase();
    }

    public ILiteCollection<TodoItem> GetTodos() => _db!.GetCollection<TodoItem>("todos");
    public ILiteCollection<Reminder> GetReminders() => _db!.GetCollection<Reminder>("reminders");
    public ILiteCollection<MemoryNote> GetMemoryNotes() => _db!.GetCollection<MemoryNote>("notes");
    public ILiteCollection<VaultItem> GetVaultItems() => _db!.GetCollection<VaultItem>("vault");
    
    public ILiteStorage<string> GetFileStorage() => _db!.FileStorage;

    public string GetCurrentDbPath() => _dbPath;

    public void Dispose()
    {
        _db?.Dispose();
    }
}
