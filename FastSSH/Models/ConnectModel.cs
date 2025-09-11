namespace FastSSH.Models
{
    public class ConnectModel
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PrivateKey { get; set; } = "";
        public string Passphrase { get; set; } = "";
        public bool UsePrivateKey { get; set; } = false;

        public string[] toArray()
        {
            return new string[] { "Name: " + Name, "Host: " + Host, "Port: " + Port.ToString(), "Username: " + Username, "Password: " + Password, "Private Key: " + PrivateKey, "Passphrase: " + Passphrase, "Use Private Key: " + UsePrivateKey.ToString() };
        }
    }
}
