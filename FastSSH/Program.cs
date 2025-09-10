using FastSSH.Models;
using FastSSH.Services;
using System.Text;

namespace FastSSH;

class Program
{
    private static readonly ConfigurationService _configService = new();

    static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 0)
            {
                ShowUsage();
                return 0;
            }
            string command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "add":
                    return await HandleAddServerAsync(args);
                case "list":
                    return await HandleListServersAsync();
                case "remove":
                    return await HandleRemoveServerAsync(args);
                case "change-password":
                case "passwd":
                    return await HandleChangePasswordAsync();
                case "help":
                case "--help":
                case "-h":
                    ShowUsage();
                    return 0;
                default:
                    // Assume it's a server name to connect to
                    return await HandleConnectAsync(args[0]);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");

            return 1;
        }
    }

    private static async Task<int> HandleConnectAsync(string serverName)
    {
        if (!_configService.ConfigurationExists())
        {
            Console.WriteLine("Нет файла конфигурации.");

            return 1;
        }

        string password = GetPassword("Введите пароль для конфигурации: ");

        try
        {
            var config = await _configService.LoadConfigurationAsync(password);
            var server = config.Servers.FirstOrDefault(s =>
                string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));

            if (server == null)
            {
                Console.WriteLine($"Сервер '{serverName}' не найден в конфигурации.");
                Console.WriteLine("Доступные серверы:");

                foreach (var srv in config.Servers)
                {
                    Console.WriteLine($"  - {srv.Name} ({srv.Username}@{srv.Host}:{srv.Port})");
                }
                return 1;
            }

            await SshService.ConnectToServerAsync(server);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка подключения: {ex.Message}");

            return 1;
        }
    }

    private static async Task<int> HandleAddServerAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Использование: fast-ssh add <имя-сервера>");

            return 1;
        }

        string serverName = args[1];

    Console.WriteLine($"Добавление новой конфигурации сервера: {serverName}");


        var server = new ServerConfig { Name = serverName };

    Console.Write("Хост: ");

        server.Host = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Порт (22): ");

        string? portInput = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(portInput) && int.TryParse(portInput, out int port))
        {
            server.Port = port;
        }

    Console.Write("Имя пользователя: ");

        server.Username = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Метод аутентификации (password/key) [password]: ");

        string authMethod = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "password";

        if (authMethod == "key")
        {
            Console.Write("Путь к приватному ключу: ");
            string? originalKeyPath = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(originalKeyPath))
            {
                Console.WriteLine("Путь к ключу не может быть пустым для аутентификации по ключу.");
                return 1;
            }

            // Expand ~ to home directory if needed
            if (originalKeyPath.StartsWith("~/"))
            {
                originalKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                               originalKeyPath.Substring(2));
            }

            if (!File.Exists(originalKeyPath))
            {
                Console.WriteLine($"Файл ключа '{originalKeyPath}' не найден.");
                return 1;
            }

            Console.Write("Пароль к ключу (оставьте пустым, если нет): ");
            server.KeyPassphrase = GetPassword("");
            if (string.IsNullOrEmpty(server.KeyPassphrase))
            {
                server.KeyPassphrase = null;
            }

            // Load existing configuration or create new one to get the password
            string configPassword;
            ServerConfigCollection config;

            if (_configService.ConfigurationExists())
            {
                configPassword = GetPassword("Введите пароль для конфигурации: ");
                config = await _configService.LoadConfigurationAsync(configPassword);
            }
            else
            {
                Console.WriteLine("Создание нового файла конфигурации.");
                configPassword = GetPassword("Установите пароль для конфигурации: ");
                string confirmPassword = GetPassword("Подтвердите пароль: ");

                if (configPassword != confirmPassword)
                {
                    Console.WriteLine("Пароли не совпадают.");
                    return 1;
                }

                config = new ServerConfigCollection();
            }

            try
            {
                // Copy key to centralized storage
                Console.WriteLine("Копирование ключа в централизованное хранилище...");
                string centralizedKeyPath = await _configService.StoreCentralizedKeyAsync(originalKeyPath, serverName);
                server.KeyFile = centralizedKeyPath;
                Console.WriteLine($"Ключ скопирован в: {centralizedKeyPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при копировании ключа: {ex.Message}");
                return 1;
            }

            // Remove existing server with same name if it exists (and clean up its key)
            var existingServer = config.Servers.FirstOrDefault(s => 
                string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));
            if (existingServer != null)
            {
                if (!string.IsNullOrEmpty(existingServer.KeyFile) && _configService.IsCentralizedKey(existingServer.KeyFile))
                {
                    _configService.RemoveCentralizedKey(existingServer.KeyFile);
                }
                config.Servers.Remove(existingServer);
            }
            
            config.Servers.Add(server);
            await _configService.SaveConfigurationAsync(config, configPassword);
        }
        else
        {
            server.Password = GetPassword("Пароль: ");
        }

        Console.Write("Описание (необязательно): ");
        server.Description = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(server.Description))
        {
            server.Description = null;
        }

        // For password authentication, handle configuration loading/creation here  
        if (authMethod != "key")
        {
            string configPassword;
            ServerConfigCollection config;

            if (_configService.ConfigurationExists())
            {
                configPassword = GetPassword("Введите пароль для конфигурации: ");
                config = await _configService.LoadConfigurationAsync(configPassword);
            }
            else
            {
                Console.WriteLine("Создание нового файла конфигурации.");
                configPassword = GetPassword("Установите пароль для конфигурации: ");
                string confirmPassword = GetPassword("Подтвердите пароль: ");

                if (configPassword != confirmPassword)
                {
                    Console.WriteLine("Пароли не совпадают.");
                    return 1;
                }

                config = new ServerConfigCollection();
            }

            // Remove existing server with same name if it exists
            config.Servers.RemoveAll(s => string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));
            config.Servers.Add(server);

            await _configService.SaveConfigurationAsync(config, configPassword);
        }

    Console.WriteLine($"Сервер '{serverName}' успешно добавлен.");
    Console.WriteLine($"Конфигурация сохранена: {_configService.GetConfigurationPath()}");


        return 0;
    }

    private static async Task<int> HandleListServersAsync()
    {
        if (!_configService.ConfigurationExists())
        {
            Console.WriteLine("Нет файла конфигурации.");

            return 1;
        }

        string password = GetPassword("Введите пароль для конфигурации: ");

        try
        {
            var config = await _configService.LoadConfigurationAsync(password);

            if (config.Servers.Count == 0)
            {
                Console.WriteLine("Нет настроенных серверов.");

                return 0;
            }

            Console.WriteLine("Настроенные серверы:");

            Console.WriteLine();

            foreach (var server in config.Servers)
            {
                Console.WriteLine($"Имя: {server.Name}");
                Console.WriteLine($"  Хост: {server.Host}:{server.Port}");
                Console.WriteLine($"  Имя пользователя: {server.Username}");
                Console.WriteLine($"  Аутентификация: {(string.IsNullOrEmpty(server.KeyFile) ? "Пароль" : $"Ключ ({server.KeyFile})")}");
                if (!string.IsNullOrEmpty(server.Description))
                {
                    Console.WriteLine($"  Описание: {server.Description}");
                }

                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Не удалось получить список серверов: {ex.Message}");

            return 1;
        }
    }

    private static async Task<int> HandleRemoveServerAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Использование: fast-ssh remove <имя-сервера>");

            return 1;
        }

        string serverName = args[1];

        if (!_configService.ConfigurationExists())
        {
            Console.WriteLine("Нет файла конфигурации.");

            return 1;
        }

        string password = GetPassword("Введите пароль для конфигурации: ");

        try
        {
            var config = await _configService.LoadConfigurationAsync(password);

            var serverToRemove = config.Servers.FirstOrDefault(s =>
                string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));

            if (serverToRemove == null)
            {
                Console.WriteLine($"Сервер '{serverName}' не найден.");
                return 1;
            }

            // Clean up centralized key file if it exists
            if (!string.IsNullOrEmpty(serverToRemove.KeyFile) && _configService.IsCentralizedKey(serverToRemove.KeyFile))
            {
                _configService.RemoveCentralizedKey(serverToRemove.KeyFile);
                Console.WriteLine("Ключ сервера удален из централизованного хранилища.");
            }

            config.Servers.Remove(serverToRemove);
            await _configService.SaveConfigurationAsync(config, password);

            Console.WriteLine($"Сервер '{serverName}' успешно удалён.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Не удалось удалить сервер: {ex.Message}");

            return 1;
        }
    }

    private static async Task<int> HandleChangePasswordAsync()
    {
        if (!_configService.ConfigurationExists())
        {
            Console.WriteLine("Нет файла конфигурации. Сначала добавьте сервер командой 'add'.");
            return 1;
        }

        string currentPassword = GetPassword("Введите текущий пароль конфигурации: ");

        try
        {
            // Try to load configuration with current password to verify it's correct
            var config = await _configService.LoadConfigurationAsync(currentPassword);

            string newPassword = GetPassword("Введите новый пароль конфигурации: ");
            string confirmPassword = GetPassword("Подтвердите новый пароль: ");

            if (newPassword != confirmPassword)
            {
                Console.WriteLine("Пароли не совпадают.");
                return 1;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                Console.WriteLine("Пароль не может быть пустым.");
                return 1;
            }

            // Save configuration with new password
            await _configService.SaveConfigurationAsync(config, newPassword);

            Console.WriteLine("Пароль конфигурации успешно изменён.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка смены пароля: {ex.Message}");
            return 1;
        }
    }

    private static void ShowUsage()
    {
    Console.WriteLine("FastSSH - Быстрый SSH-клиент с шифрованным хранилищем конфигурации");

        Console.WriteLine();
    Console.WriteLine("Использование:");
    Console.WriteLine("  fast-ssh <server-name>     Подключиться к серверу");
    Console.WriteLine("  fast-ssh add <server-name> Добавить или обновить конфигурацию сервера");
    Console.WriteLine("  fast-ssh list              Показать все серверы");
    Console.WriteLine("  fast-ssh remove <name>     Удалить сервер");
    Console.WriteLine("  fast-ssh change-password   Сменить пароль конфигурации");
    Console.WriteLine("  fast-ssh help              Показать справку");
    Console.WriteLine();
    Console.WriteLine("Примеры:");
    Console.WriteLine("  fast-ssh myserver          Подключиться к 'myserver'");
    Console.WriteLine("  fast-ssh add prod-server   Добавить новый сервер");
    Console.WriteLine("  fast-ssh list              Показать все серверы");
    Console.WriteLine();
    Console.WriteLine("Конфигурация хранится в:");
    Console.WriteLine($"  {_configService.GetConfigurationPath()}");
    Console.WriteLine("Использование:");
    Console.WriteLine("  fast-ssh <server-name>     Подключиться к серверу");
    Console.WriteLine("  fast-ssh add <server-name> Добавить или обновить конфигурацию сервера");
    Console.WriteLine("  fast-ssh list              Показать все серверы");
    Console.WriteLine("  fast-ssh remove <name>     Удалить сервер");
    Console.WriteLine("  fast-ssh change-password   Сменить пароль конфигурации");
    Console.WriteLine("  fast-ssh help              Показать справку");
    Console.WriteLine();
    Console.WriteLine("Примеры:");
    Console.WriteLine("  fast-ssh myserver          Подключиться к 'myserver'");
    Console.WriteLine("  fast-ssh add prod-server   Добавить новый сервер");
    Console.WriteLine("  fast-ssh list              Показать все серверы");
    Console.WriteLine();
    Console.WriteLine("Конфигурация хранится в:");
    Console.WriteLine($"  {_configService.GetConfigurationPath()}");

    }

    private static string GetPassword(string prompt)
    {
        Console.Write(prompt);

        var password = new StringBuilder();
        ConsoleKeyInfo keyInfo;

        do
        {
            keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (keyInfo.Key != ConsoleKey.Enter && keyInfo.Key != ConsoleKey.Backspace && !char.IsControl(keyInfo.KeyChar))
            {
                password.Append(keyInfo.KeyChar);
                Console.Write("*");
            }
        } while (keyInfo.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password.ToString();
    }
}
