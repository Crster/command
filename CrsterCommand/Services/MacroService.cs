using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrsterCommand.Services;

public class MacroService
{
    public string GenerateRandomPassword(int length = 16)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public bool IsAdmin()
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        return false; // For Linux/Mac, usually requires sudo which is handled differently
    }

    public async Task<string> ResetNetworkAsync()
    {
        if (OperatingSystem.IsWindows() && !IsAdmin())
        {
            // Request elevation by restarting the command with 'runas'
            return await Task.Run(() => 
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"ipconfig /flushdns; ipconfig /release; ipconfig /renew\"",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                    Process.Start(psi)?.WaitForExit();
                    return "Network Reset request sent (via UAC prompt).";
                }
                catch (Exception ex)
                {
                    return "Elevation failed or cancelled: " + ex.Message;
                }
            });
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "bash",
                Arguments = OperatingSystem.IsWindows() 
                    ? "-Command \"ipconfig /flushdns; ipconfig /release; ipconfig /renew\"" 
                    : "-c \"sudo ipconfig /flushdns; sudo dhclient -r; sudo dhclient\"",
                RedirectStandardOutput = !OperatingSystem.IsWindows() || IsAdmin(),
                UseShellExecute = !OperatingSystem.IsWindows() && !IsAdmin(),
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return "Failed to start process.";
            
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return "Network Reset Successful.\n" + output;
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }
}
