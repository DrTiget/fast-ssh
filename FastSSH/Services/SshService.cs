using FastSSH.Models;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace FastSSH.Services;

public class SshService
{
    public static async Task ConnectToServerAsync(ServerConfig server)
    {
        try
        {
            ConnectionInfo connectionInfo = CreateConnectionInfo(server);
            
            using var client = new SshClient(connectionInfo);
            
            Console.WriteLine($"Connecting to {server.Name} ({server.Host}:{server.Port})...");
            
            await Task.Run(() => client.Connect());
            
            if (!client.IsConnected)
            {
                throw new InvalidOperationException("Failed to establish SSH connection");
            }

            Console.WriteLine($"Connected successfully to {server.Name}!");
            Console.WriteLine("Starting interactive shell...");
            Console.WriteLine("Type 'exit' or press Ctrl+C to disconnect.\n");

            // Start interactive shell
            await StartInteractiveShellAsync(client);
        }
        catch (SshConnectionException ex)
        {
            throw new InvalidOperationException($"SSH connection failed: {ex.Message}", ex);
        }
        catch (SshAuthenticationException ex)
        {
            throw new InvalidOperationException($"SSH authentication failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Connection error: {ex.Message}", ex);
        }
    }

    private static ConnectionInfo CreateConnectionInfo(ServerConfig server)
    {
        List<AuthenticationMethod> authMethods = new();

        // Key-based authentication
        if (!string.IsNullOrEmpty(server.KeyFile) && File.Exists(server.KeyFile))
        {
            try
            {
                PrivateKeyFile keyFile;
                if (!string.IsNullOrEmpty(server.KeyPassphrase))
                {
                    keyFile = new PrivateKeyFile(server.KeyFile, server.KeyPassphrase);
                }
                else
                {
                    keyFile = new PrivateKeyFile(server.KeyFile);
                }
                
                authMethods.Add(new PrivateKeyAuthenticationMethod(server.Username, keyFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load key file {server.KeyFile}: {ex.Message}");
            }
        }

        // Password-based authentication
        if (!string.IsNullOrEmpty(server.Password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(server.Username, server.Password));
        }

        if (authMethods.Count == 0)
        {
            throw new InvalidOperationException("No valid authentication method configured for server");
        }

        return new ConnectionInfo(server.Host, server.Port, server.Username, authMethods.ToArray());
    }

    private static async Task StartInteractiveShellAsync(SshClient client)
    {
        using var shell = client.CreateShell(Console.OpenStandardInput(), 
                                           Console.OpenStandardOutput(), 
                                           Console.OpenStandardError());

        var shellStopped = new TaskCompletionSource<bool>();

        shell.Stopped += (sender, e) =>
        {
            shellStopped.TrySetResult(true);
        };

        shell.Start();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            shell.Stop();
        };

        // Ожидание завершения shell
        await shellStopped.Task;

        Console.WriteLine("\nDisconnected from server.");
    }
}