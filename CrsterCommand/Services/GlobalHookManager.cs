using System;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace CrsterCommand.Services;

public sealed class GlobalHookManager : IDisposable
{
    private readonly IGlobalHook _hook;
    private bool _started;
    private bool _disposed;
    private Task? _hookTask;
    private static GlobalHookManager? _instance;
    private static readonly object _syncInstance = new();

    private GlobalHookManager()
    {
        _hook = new EventLoopGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
    }

    public static GlobalHookManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncInstance)
                {
                    _instance ??= new GlobalHookManager();
                }
            }
            return _instance;
        }
    }

    public static void ResetInstance()
    {
        if (_instance != null)
        {
            lock (_syncInstance)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }
    }

    public event EventHandler<KeyboardHookEventArgs>? KeyPressed;

    public void Start()
    {
        if (_started)
        {
            Console.WriteLine("[GlobalHookManager] Hook already started");
            return;
        }

        Console.WriteLine("[GlobalHookManager] Starting global hook");
        _started = true;
        _hookTask = _hook.RunAsync();
        Console.WriteLine("[GlobalHookManager] Hook started async");
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (!_disposed)
        {
            KeyPressed?.Invoke(this, e);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            Console.WriteLine("[GlobalHookManager] Already disposed");
            return;
        }

        try
        {
            Console.WriteLine("[GlobalHookManager] Disposing...");
            _disposed = true;
            _started = false;

            _hook.KeyPressed -= OnKeyPressed;

            // Try to stop the hook gracefully
            try
            {
                _hook.Dispose();
                Console.WriteLine("[GlobalHookManager] Hook disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GlobalHookManager] Error disposing hook: {ex.Message}");
            }

            // Wait a bit for the hook task to stop
            if (_hookTask != null && !_hookTask.IsCompleted)
            {
                Console.WriteLine("[GlobalHookManager] Waiting for hook task to complete...");
                if (_hookTask.Wait(TimeSpan.FromSeconds(2)))
                {
                    Console.WriteLine("[GlobalHookManager] Hook task completed");
                }
                else
                {
                    Console.WriteLine("[GlobalHookManager] Hook task did not complete in time");
                }
            }

            Console.WriteLine("[GlobalHookManager] Disposed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GlobalHookManager] Error during dispose: {ex.Message}");
        }
    }
}
