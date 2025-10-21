namespace EdgegapProxy.Models;

/// <summary>
/// Server information returned to Unity clients.
/// </summary>
public class ServerInfo
{
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public int HealthPort { get; set; } // External health check port from Edgegap
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public int PingMs { get; set; }
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Get connection address (IP:Port format).
    /// </summary>
    public string GetConnectionAddress() => $"{IpAddress}:{Port}";

    /// <summary>
    /// Get display name for UI (e.g., "Chicago (5/300 players) - 45ms").
    /// </summary>
    public string GetDisplayName()
    {
        if (!IsHealthy)
            return $"{City} - Offline";

        return $"{City} ({CurrentPlayers}/{MaxPlayers} players) - {PingMs}ms";
    }
}

/// <summary>
/// Edgegap deployment response model.
/// </summary>
public class EdgegapDeploymentResponse
{
    public List<EdgegapDeployment> Data { get; set; } = new();
}

/// <summary>
/// Edgegap deployment model.
/// </summary>
public class EdgegapDeployment
{
    [System.Text.Json.Serialization.JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("fqdn")]
    public string Fqdn { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("public_ip")]
    public string PublicIp { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("current_status")]
    public string CurrentStatus { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("ports")]
    public Dictionary<string, EdgegapPort> Ports { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Edgegap port mapping model.
/// </summary>
public class EdgegapPort
{
    [System.Text.Json.Serialization.JsonPropertyName("external")]
    public int External { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("internal")]
    public int Internal { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;
}

/// <summary>
/// Health check response from game server.
/// </summary>
public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Uptime { get; set; }
    public long Timestamp { get; set; }
}
