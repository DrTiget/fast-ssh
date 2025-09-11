using FastSSH.Models;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace FastSSH.Services
{
    class ConnectionsService
    {
        public static List<ConnectModel> LoadConnections(string Password, UserConfig userConfig)
        {
            var connections = new List<ConnectModel>();
            var connectionsDir = getConnectionsDirName(userConfig);
            createConnectionsDirIfNotExists(connectionsDir);
            if (userConfig.ConnectionsStorageMode != "local") {
                SyncService.SyncConnections(userConfig);
            }
            var binFiles = Directory.GetFiles(connectionsDir, "*.bin");
            foreach (var binFile in binFiles)
            {
                var rawConnection = File.ReadAllText(binFile);
                var connectionJson = EncryptionService.Decrypt(rawConnection, Password);
                if (!string.IsNullOrEmpty(connectionJson))
                { 
                    var connection = JsonConvert.DeserializeObject<ConnectModel>(connectionJson);
                    if (connection != null)
                    {
                        connections.Add(connection);
                    }
                }
            }
            return connections;
        }

        public static void SaveConnections(List<ConnectModel> connections, string Password, UserConfig userConfig)
        {
            var connectionsDir = getConnectionsDirName(userConfig);
            createConnectionsDirIfNotExists(connectionsDir);
            var existingFiles = new HashSet<string>(Directory.GetFiles(connectionsDir, "*.bin"));
            foreach (var connection in connections)
            {
                var connectionJson = JsonConvert.SerializeObject(connection);
                var encryptedConnection = EncryptionService.Encrypt(connectionJson, Password);
                var safeFileName = string.Join("_", connection.Name.Split(Path.GetInvalidFileNameChars()));
                var filePath = Path.Combine(connectionsDir, $"{safeFileName}.bin");
                File.WriteAllText(filePath, encryptedConnection);
                existingFiles.Remove(filePath);
            }
            // Удаляем файлы, которые больше не нужны
            foreach (var obsoleteFile in existingFiles)
            {
                File.Delete(obsoleteFile);
            }
            if (userConfig.ConnectionsStorageMode != "local")
            {
                SyncService.SyncConnections(userConfig);
            }
        }

        private static string getConnectionsDirName(UserConfig userConfig)
        {
            string connectionsDirName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                connectionsDirName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastSSH", "connections");
            }
            else
            {
                connectionsDirName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "fast-ssh", "connections");
            }
            Directory.CreateDirectory(connectionsDirName);
            return connectionsDirName;
        }

        private static void createConnectionsDirIfNotExists(string dirPath)
        {
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
        }
    }
}

