using FastSSH.Models;
using FastSSH.Services;
using System.Text;

namespace FastSSH;

class Program
{
    private static UserConfig userConfig = new UserConfig();
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        userConfig = UserConfigService.LoadConfig();
        if (args.Length == 0)
        {
            return ShowUsage();
        }
        string command = args[0].ToLowerInvariant();
        string Password;
        switch (command)
        {
            case "list":
                Password = GetPassword();
                return ShowConnections(Password);
            case "add":
                Password = GetPassword();
                return AddConnection(Password);
            case "remove":
                Password = GetPassword();
                return DeleteConnection(Password, args);
            case "config":
                return ShowConfig();
            case "change-password":
                return ChangePasswordAndReEncryptConnections();
            case "change-storage":
                return ChangeStorageMode();

            case "help":
            case "--help":
            case "-h":
                return ShowUsage();
            default:
                return ConnectToServer(args);
        }
    }

    private static int ConnectToServer(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Пожалуйста, укажите имя сервера для подключения.");
            return 1;
        }
        string name = args[0];
        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("Пожалуйста, укажите имя сервера для подключения.");
            return 1;
        }
        string Password = GetPassword();
        var connections = ConnectionsService.LoadConnections(Password, userConfig);
        var connection = connections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (connection == null)
        {
            Console.WriteLine($"Подключение с именем '{name}' не найдено.");
            return 1;
        }
        try
        {
            string hint = "";
            if (connection.UsePrivateKey && !string.IsNullOrEmpty(connection.Passphrase))
                hint = "Пароль к приватному ключу: " + connection.Passphrase;
            if (connection.Password != "")
                hint += (hint != "" ? "; " : "") + "Пароль к серверу: " + connection.Password;
            SshService.ConnectWithSystemSsh(connection, hint);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка подключения: {ex.Message}");
            return 1;
        }
    }

    private static int ChangeStorageMode()
    {
        Console.WriteLine("Выберите способ хранения подключений:");
        Console.WriteLine("1. Локально (local)");
        Console.WriteLine("2. SSH-Hub (ssh-hub)");
        Console.WriteLine("3. Self-Hosted (self-hosted)");
        Console.Write("Введите номер варианта (1-3): ");
        string choice = Console.ReadLine() ?? "1";
        switch (choice)
        {
            case "1":
                userConfig.ConnectionsStorageMode = "local";
                break;
            case "2":
                userConfig.ConnectionsStorageMode = "ssh-hub";
                break;
            case "3":
                userConfig.ConnectionsStorageMode = "self-hosted";
                Console.Write("Введите URL self-hosted сервера: ");
                userConfig.SelfHostedUrl = Console.ReadLine() ?? "";
                break;
            default:
                Console.WriteLine("Неверный выбор. Способ хранения не изменен.");
                return 1;
        }
        UserConfigService.SaveConfig(userConfig);
        Console.WriteLine($"Способ хранения подключений изменен на: {userConfig.ConnectionsStorageMode}");
        if (userConfig.ConnectionsStorageMode != "local")
        {
            SyncService.SyncConnections(userConfig);
            Console.WriteLine("Подключения синхронизированы с удаленным сервером.");
        }
        return 0;
    }

    private static int ChangePasswordAndReEncryptConnections()
    {
        string oldPassword = GetPassword();
        string newPassword1 = GetPassword("Введите новый пароль: ");
        string newPassword2 = GetPassword("Повторите новый пароль: ");
        if (newPassword1 != newPassword2)
        {
            Console.WriteLine("Пароли не совпадают. Пароль не изменен.");
            return 1;
        }
        var connections = ConnectionsService.LoadConnections(oldPassword, userConfig);
        ConnectionsService.SaveConnections(connections, newPassword1, userConfig);
        Console.WriteLine("Пароль успешно изменен.");
        Console.WriteLine("Подключения пере-шифрованы.");
        return 0;
    }

    private static int ShowConfig()
    {
        Console.WriteLine("Текущие настройки:");
        Console.WriteLine($"  Способ хранения подключений: {userConfig.ConnectionsStorageMode}");
        if (userConfig.ConnectionsStorageMode == "self-hosted")
        {
            Console.WriteLine($"  URL self-hosted сервера: {userConfig.SelfHostedUrl}");
        }
        return 0;
    }

    private static int DeleteConnection(string Password, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Пожалуйста, укажите имя подключения для удаления.");
            return 1;
        }
        string name = args[1];
        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("Пожалуйста, укажите имя подключения для удаления.");
            return 1;
        }
        var connections = ConnectionsService.LoadConnections(Password, userConfig);
        var connectionToRemove = connections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (connectionToRemove != null)
        {
            connections.Remove(connectionToRemove);
            ConnectionsService.SaveConnections(connections, Password, userConfig);
            Console.WriteLine($"Подключение '{name}' удалено.");
        }
        else
        {
            Console.WriteLine($"Подключение с именем '{name}' не найдено.");
        }
        return 0;
    }

    private static int AddConnection(string Password)
    {
        Console.Write("Введите имя подключения: ");
        string name = Console.ReadLine() ?? "";
        Console.Write("Введите хост (IP или домен): ");
        string host = Console.ReadLine() ?? "";
        Console.Write("Введите порт (по умолчанию 22): ");
        string portInput = Console.ReadLine() ?? "22";
        int port = int.TryParse(portInput, out int p) ? p : 22;
        Console.Write("Введите имя пользователя: ");
        string username = Console.ReadLine() ?? "";
        Console.Write("Введите путь к приватному ключу (или оставьте пустым для пароля): ");
        string privateKeyPath = Console.ReadLine() ?? "";
        string privateKey = "";
        ConsoleKeyInfo keyInfo;
        string passphrase = "";
        if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
        {
            privateKey = File.ReadAllText(privateKeyPath);
            Console.Write("Введите пароль к приватному ключу (если есть, иначе оставьте пустым): ");
            StringBuilder passphraseBuilder = new StringBuilder();
            do
            {
                keyInfo = Console.ReadKey(intercept: true);
                if (keyInfo.Key != ConsoleKey.Backspace && keyInfo.Key != ConsoleKey.Enter)
                {
                    passphraseBuilder.Append(keyInfo.KeyChar);
                    Console.Write("*");
                }
                else if (keyInfo.Key == ConsoleKey.Backspace && passphraseBuilder.Length > 0)
                {
                    passphraseBuilder.Remove(passphraseBuilder.Length - 1, 1);
                    Console.Write("\b \b");
                }
            } while (keyInfo.Key != ConsoleKey.Enter);
            passphrase = passphraseBuilder.ToString();
            Console.WriteLine();
        }
        else if (!string.IsNullOrEmpty(privateKeyPath))
        {
            Console.WriteLine("Файл приватного ключа не найден. Подключение не будет сохранено.");
            return 0;
        }
        Console.Write("Введите пароль (оставьте пустым, если используете ключ): ");
        StringBuilder passwordBuilder = new StringBuilder();
        do
        {
            keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key != ConsoleKey.Backspace && keyInfo.Key != ConsoleKey.Enter)
            {
                passwordBuilder.Append(keyInfo.KeyChar);
                Console.Write("*");
            }
            else if (keyInfo.Key == ConsoleKey.Backspace && passwordBuilder.Length > 0)
            {
                passwordBuilder.Remove(passwordBuilder.Length - 1, 1);
                Console.Write("\b \b");
            }
        } while (keyInfo.Key != ConsoleKey.Enter);
        string password = passwordBuilder.ToString();
        Console.WriteLine();

        var newConnection = new ConnectModel
        {
            Name = name,
            Host = host,
            Port = port,
            Username = username,
            PrivateKey = privateKey,
            Passphrase = passphrase,
            Password = password,
            UsePrivateKey = !string.IsNullOrEmpty(privateKey)
        };
        var connections = ConnectionsService.LoadConnections(Password, userConfig);
        connections.Add(newConnection);
        ConnectionsService.SaveConnections(connections, Password, userConfig);
        Console.WriteLine($"Подключение '{name}' сохранено.");
        return 0;
    }

    private static int ShowConnections(string Password)
    {
        var connections = ConnectionsService.LoadConnections(Password, userConfig);
        if (connections.Count == 0)
        {
            Console.WriteLine("Нет сохраненных подключений.");
            return 0;
        }
        Console.WriteLine("Сохраненные подключения:");
        foreach (var conn in connections)
        {
            Console.WriteLine($"- {conn.Name} ({conn.Host}:{conn.Port})");
        }
        return 0;
    }

    private static string GetPassword(string prompt = "Введите пароль приложения: ")
    {
        Console.Write(prompt);
        StringBuilder passwordBuilder = new StringBuilder();
        ConsoleKeyInfo keyInfo;
        do
        {
            keyInfo = Console.ReadKey(intercept: true);
            if (keyInfo.Key != ConsoleKey.Backspace && keyInfo.Key != ConsoleKey.Enter)
            {
                passwordBuilder.Append(keyInfo.KeyChar);
                Console.Write("*");
            }
            else if (keyInfo.Key == ConsoleKey.Backspace && passwordBuilder.Length > 0)
            {
                passwordBuilder.Remove(passwordBuilder.Length - 1, 1);
                Console.Write("\b \b");
            }
        } while (keyInfo.Key != ConsoleKey.Enter);
        Console.WriteLine();
        return passwordBuilder.ToString();
    }
    private static int ShowUsage()
    {
        Console.WriteLine("Использование:");
        Console.WriteLine("  fast-ssh <server-name>     Подключиться к серверу");
        Console.WriteLine("  fast-ssh add               Добавить сервер");
        Console.WriteLine("  fast-ssh list              Показать список серверов");
        Console.WriteLine("  fast-ssh remove <name>     Удалить сервер");
        Console.WriteLine("  fast-ssh help              Показать это сообщение");
        Console.WriteLine();
        Console.WriteLine("Настройки:");
        Console.WriteLine("  fast-ssh config            Показать настройки");
        Console.WriteLine("  fast-ssh change-password   Изменить пароль");
        Console.WriteLine("  fast-ssh change-storage    Изменить способ хранения подключений (локально, ssh-hub, self-hosted)");
        return 0;
    }

    // static async Task<int> Main(string[] args)
    // {
    //     try
    //     {
    //         Console.OutputEncoding = Encoding.UTF8;
    //         if (args.Length == 0)
    //         {
    //             ShowUsage();
    //             return 0;
    //         }
    //         string command = args[0].ToLowerInvariant();
    //         switch (command)
    //         {
    //             case "add":
    //                 return await HandleAddServerAsync(args);
    //             case "list":
    //                 return await HandleListServersAsync();
    //             case "remove":
    //                 return await HandleRemoveServerAsync(args);
    //             case "change-password":
    //             case "passwd":
    //                 return await HandleChangePasswordAsync();
    //             case "help":
    //             case "--help":
    //             case "-h":
    //                 ShowUsage();
    //                 return 0;
    //             default:
    //                 return await HandleConnectAsync(args[0]);
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.Error.WriteLine($"Ошибка: {ex.Message}");

    //         return 1;
    //     }
    // }

    // private static void ShowUsage()
    // {
    //     Console.WriteLine("Использование:");
    //     Console.WriteLine("  fast-ssh <server-name>     Подключиться к серверу");
    // }

    // private static Task<int> HandleAddServerAsync(string[] args)
    // {
    //     return AddServerCommand.ExecuteAsync(args);
    // }

}
