namespace FastSSH.Models
{
    public class UserConfig
    {
        public string ConnectionsStorageMode { get; set; } = "local"; // "local", "ssh-hub", "self-hosted"
        public string SelfHostedUrl { get; set; } = "";

        public UserConfig()
        {
        }
    }
}