using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FastSSH.Models;

namespace FastSSH.Services;

public static class SshService
{
    public static int ConnectWithSystemSsh(ConnectModel server, string? keywordHint = null)
    {
        if (!string.IsNullOrWhiteSpace(keywordHint))
            Console.WriteLine($"Подсказка: {keywordHint}");

        string? keyPath = null;
        try
        {
            // 1) Сохраняем приватный ключ (если задан строкой)
            if (!string.IsNullOrWhiteSpace(server.PrivateKey))
            {
                keyPath = WriteTempPrivateKey(server.PrivateKey);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    HardenKeyOnWindows(keyPath);
                else
                    Chmod600(keyPath);
            }

            // 2) Собираем аргументы и запускаем "ssh" БЕЗ shell
            var psi = new ProcessStartInfo("ssh") { UseShellExecute = false };
            psi.ArgumentList.Add("-tt"); // форсируем PTY для TUI

            if (server.Port > 0 && server.Port != 22)
            {
                psi.ArgumentList.Add("-p");
                psi.ArgumentList.Add(server.Port.ToString());
            }

            if (!string.IsNullOrEmpty(keyPath))
            {
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(keyPath); // никаких кавычек!
            }

            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add("StrictHostKeyChecking=accept-new");

            psi.ArgumentList.Add($"{server.Username}@{server.Host}");

            Console.WriteLine($"→ Запускаю системный ssh: {server.Username}@{server.Host}:{server.Port}");

            int exitCode;
            using (var p = Process.Start(psi)!)
            {
                p.WaitForExit();
                exitCode = p.ExitCode;
            }

            return exitCode;
        }
        finally
        {
            // 3) Удаляем временный ключ безопасно (Linux: сначала shred, затем rm/File.Delete)
            if (!string.IsNullOrEmpty(keyPath))
                SafeDeleteKey(keyPath);
        }
    }

    // ---------- helpers ----------

    private static string WriteTempPrivateKey(string privateKey)
    {
        var dir = Path.Combine(Path.GetTempPath(), "fastssh");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"fastssh_key_{Guid.NewGuid():N}");
        File.WriteAllText(path, privateKey, new UTF8Encoding(false)); // без BOM
        return path;
    }

    private static void Chmod600(string path)
    {
        try
        {
            var psi = new ProcessStartInfo("/bin/chmod") { UseShellExecute = false };
            psi.ArgumentList.Add("600");
            psi.ArgumentList.Add(path);
            Process.Start(psi)?.WaitForExit();
        }
        catch { /* best-effort */ }
    }

    private static void HardenKeyOnWindows(string path)
    {
        try
        {
            var who = RunCmdCapture("whoami").Trim();
            if (string.IsNullOrEmpty(who)) who = Environment.UserName;

            RunCmd($@"icacls ""{path}"" /inheritance:r");
            RunCmd($@"icacls ""{path}"" /grant:r ""{who}"":F");
        }
        catch { /* best-effort */ }
    }

    private static void SafeDeleteKey(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: просто удалить
                if (File.Exists(path)) File.Delete(path);
                return;
            }

            // Linux/macOS: сначала пробуем shred -u, затем обычное удаление
            try
            {
                var psi = new ProcessStartInfo("shred") { UseShellExecute = false };
                psi.ArgumentList.Add("-u");
                psi.ArgumentList.Add("--");
                psi.ArgumentList.Add(path);
                using var p = Process.Start(psi);
                p?.WaitForExit();
                if (p?.ExitCode == 0) return;
            }
            catch { /* shred может отсутствовать — это нормально */ }

            // Fallback
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // на крайний случай оставим файл — лучше оставить, чем уронить приложение
        }
    }

    private static void RunCmd(string cmd)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", "/C " + cmd)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })?.WaitForExit();
    }

    private static string RunCmdCapture(string cmd)
    {
        var psi = new ProcessStartInfo("cmd.exe", "/C " + cmd)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var s = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return s;
    }
}
