using Newtonsoft.Json;

namespace FastSSH.Models;

public class ServerConfig
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("host")]
    public string Host { get; set; } = string.Empty;

    [JsonProperty("port")]
    public int Port { get; set; } = 22;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("password")]
    public string? Password { get; set; }

    [JsonProperty("keyFile")]
    public string? KeyFile { get; set; }

    [JsonProperty("keyPassphrase")]
    public string? KeyPassphrase { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
}

public class ServerConfigCollection
{
    [JsonProperty("servers")]
    public List<ServerConfig> Servers { get; set; } = new();
}