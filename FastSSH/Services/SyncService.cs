using FastSSH.Models;
using FastSSH.Services;
namespace FastSSH.Services
{
    public class SyncService
    {
        public static void SyncConnections(UserConfig userConfig)
        {
            if (userConfig.ConnectionsStorageMode == "local")
            {
                return;
            }
            if (userConfig.ConnectionsStorageMode == "ssh-hub")
            {
                string sshHubUrl = "https://ssh-hub.com";
            }
            else if (userConfig.ConnectionsStorageMode == "self-hosted")
            {
                string selfHostedUrl = userConfig.SelfHostedUrl;
            }   
        }
    }
}