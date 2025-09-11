using FastSSH.Models;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace FastSSH.Services
{
    class UserConfigService
    {
        private static string getConfigDirName()
        {
            string configDirName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                configDirName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastSSH");
            }
            else
            {
                configDirName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "fast-ssh");
            }
            return configDirName;
        }

        private static void createConfigDirIfNotExists(string configDirName)
        {
            if (!Directory.Exists(configDirName))
            {
                Directory.CreateDirectory(configDirName);
            }
        }

        public static UserConfig LoadConfig()
        {
            string configDirName = getConfigDirName();
            createConfigDirIfNotExists(configDirName);
            string configFileName = Path.Combine(configDirName, "config.json");
            if (File.Exists(configFileName))
            {
                string json = File.ReadAllText(configFileName);
                var config = JsonConvert.DeserializeObject<UserConfig>(json);
                if (config != null)
                {
                    return config;
                }
            }
            return new UserConfig();
        }

        public static void SaveConfig(UserConfig config)
        {
            string configDirName = getConfigDirName();
            createConfigDirIfNotExists(configDirName);
            string configFileName = Path.Combine(configDirName, "config.json");
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFileName, json);
        }
    }
}