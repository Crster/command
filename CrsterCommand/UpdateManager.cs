using System;
using Velopack;

namespace CrsterCommand;

public static class UpdateManager
{
    public static void Initialize()
    {
        // Simple Velopack implementation for Windows/Mac
        VelopackApp.Build()
            .Run();
    }
}
