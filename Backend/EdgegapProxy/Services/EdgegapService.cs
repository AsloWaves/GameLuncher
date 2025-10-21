using System.Net.Http.Headers;
using System.Text.Json;
using EdgegapProxy.Models;

namespace EdgegapProxy.Services;

/// <summary>
/// Service for interacting with Edgegap API and game server health endpoints.
/// Keeps API token secure on server-side.
/// </summary>
public class EdgegapService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EdgegapService> _logger;

    private string ApiToken => _configuration["Edgegap:ApiToken"] ?? throw new InvalidOperationException("Edgegap API token not configured");
    private string ApiBaseUrl => _configuration["Edgegap:ApiBaseUrl"] ?? "https://api.edgegap.com/v1";
    private int HealthCheckPort => _configuration.GetValue<int>("Edgegap:HealthCheckPort", 8080);
    private int HealthCheckTimeout => _configuration.GetValue<int>("Edgegap:HealthCheckTimeout", 5);
    private string[] RequiredTags => _configuration.GetSection("Edgegap:RequiredTags").Get<string[]>() ?? Array.Empty<string>();

    public EdgegapService(HttpClient httpClient, IConfiguration configuration, ILogger<EdgegapService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Configure HTTP client for Edgegap API
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", ApiToken);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Fetch all active deployments from Edgegap API.
    /// </summary>
    public async Task<List<ServerInfo>> GetActiveServersAsync()
    {
        try
        {
            _logger.LogInformation("Fetching active deployments from Edgegap...");

            // Call Edgegap API
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/deployments");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var deploymentsResponse = JsonSerializer.Deserialize<EdgegapDeploymentResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (deploymentsResponse == null || deploymentsResponse.Data == null)
            {
                _logger.LogWarning("No deployments found in Edgegap response");
                return new List<ServerInfo>();
            }

            _logger.LogInformation($"Found {deploymentsResponse.Data.Count} deployments from Edgegap");

            // Filter by tags
            var filteredDeployments = FilterByTags(deploymentsResponse.Data);
            _logger.LogInformation($"Filtered to {filteredDeployments.Count} deployments matching required tags");

            // Convert to ServerInfo and validate health
            var servers = new List<ServerInfo>();
            foreach (var deployment in filteredDeployments)
            {
                var serverInfo = ConvertToServerInfo(deployment);
                if (serverInfo != null)
                {
                    // Validate server health
                    await ValidateServerHealthAsync(serverInfo);
                    servers.Add(serverInfo);
                }
            }

            // Sort by health, then player count, then ping
            servers = servers
                .OrderByDescending(s => s.IsHealthy)
                .ThenByDescending(s => s.CurrentPlayers)
                .ThenBy(s => s.PingMs)
                .ToList();

            _logger.LogInformation($"Returning {servers.Count} validated servers ({servers.Count(s => s.IsHealthy)} healthy)");

            return servers;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch deployments from Edgegap API");
            throw new InvalidOperationException("Failed to communicate with Edgegap API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching active servers");
            throw;
        }
    }

    /// <summary>
    /// Filter deployments by required tags.
    /// </summary>
    private List<EdgegapDeployment> FilterByTags(List<EdgegapDeployment> deployments)
    {
        if (RequiredTags.Length == 0)
            return deployments;

        return deployments.Where(d =>
        {
            if (d.Tags == null || d.Tags.Count == 0)
                return false;

            // Check if deployment has ALL required tags
            return RequiredTags.All(requiredTag => d.Tags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase));
        }).ToList();
    }

    /// <summary>
    /// Convert Edgegap deployment to ServerInfo model.
    /// </summary>
    private ServerInfo? ConvertToServerInfo(EdgegapDeployment deployment)
    {
        try
        {
            // Find game port (UDP protocol) - works regardless of port name
            var gameport = deployment.Ports.Values.FirstOrDefault(p =>
                p.Protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase));

            if (gameport == null)
            {
                _logger.LogWarning($"Deployment {deployment.RequestId} missing UDP game port");
                return null;
            }

            // Find health port (TCP on internal port 8080) - works regardless of port name
            var healthPort = HealthCheckPort; // Default fallback
            var healthPortMapping = deployment.Ports.Values.FirstOrDefault(p =>
                p.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase) &&
                p.Internal == 8080);

            if (healthPortMapping != null)
            {
                healthPort = healthPortMapping.External;
                _logger.LogDebug($"Found health port mapping: {healthPortMapping.Internal} → {healthPort}");
            }
            else
            {
                _logger.LogWarning($"Deployment {deployment.RequestId} missing TCP health port on 8080, using default {HealthCheckPort}");
            }

            return new ServerInfo
            {
                ServerId = deployment.RequestId,
                ServerName = $"{deployment.City} Server",
                IpAddress = deployment.PublicIp,
                Port = gameport.External,
                HealthPort = healthPort,
                Region = deployment.Country,
                City = deployment.City,
                Country = deployment.Country,
                Status = deployment.CurrentStatus,
                Tags = deployment.Tags.ToArray(),
                IsHealthy = false, // Will be validated
                PingMs = 9999 // Will be measured
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to convert deployment {deployment.RequestId} to ServerInfo");
            return null;
        }
    }

    /// <summary>
    /// Validate server health by calling HTTP health endpoint.
    /// </summary>
    private async Task ValidateServerHealthAsync(ServerInfo server)
    {
        try
        {
            var healthUrl = $"http://{server.IpAddress}:{server.HealthPort}/health";
            _logger.LogDebug($"Checking health: {healthUrl}");

            var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(HealthCheckTimeout) };

            var startTime = DateTime.UtcNow;
            var response = await healthClient.GetAsync(healthUrl);
            var pingMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var healthResponse = JsonSerializer.Deserialize<HealthCheckResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (healthResponse != null)
                {
                    server.CurrentPlayers = healthResponse.Players;
                    server.MaxPlayers = healthResponse.MaxPlayers;
                    server.IsHealthy = true;
                    server.PingMs = pingMs;

                    _logger.LogInformation($"✅ {server.City} - Healthy ({server.CurrentPlayers} players, {server.PingMs}ms)");
                }
            }
            else
            {
                server.IsHealthy = false;
                _logger.LogWarning($"❌ {server.City} - Health check failed: HTTP {response.StatusCode}");
            }
        }
        catch (TaskCanceledException)
        {
            server.IsHealthy = false;
            _logger.LogWarning($"❌ {server.City} - Health check timeout");
        }
        catch (Exception ex)
        {
            server.IsHealthy = false;
            _logger.LogWarning(ex, $"❌ {server.City} - Health check error: {ex.Message}");
        }
    }
}
