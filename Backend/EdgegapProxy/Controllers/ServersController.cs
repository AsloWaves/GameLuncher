using Microsoft.AspNetCore.Mvc;
using EdgegapProxy.Models;
using EdgegapProxy.Services;

namespace EdgegapProxy.Controllers;

/// <summary>
/// API controller for Unity clients to fetch server list.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServersController : ControllerBase
{
    private readonly EdgegapService _edgegapService;
    private readonly ILogger<ServersController> _logger;

    // Cache server list for 30 seconds to avoid hammering Edgegap API
    private static List<ServerInfo>? _cachedServers;
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public ServersController(EdgegapService edgegapService, ILogger<ServersController> logger)
    {
        _edgegapService = edgegapService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/servers - List all active game servers.
    /// </summary>
    /// <param name="forceRefresh">Optional: bypass cache and force refresh from Edgegap</param>
    /// <returns>List of available game servers with health status</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ServerInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ServerInfo>>> GetServers([FromQuery] bool forceRefresh = false)
    {
        try
        {
            _logger.LogInformation($"GET /api/servers (forceRefresh={forceRefresh})");

            // Check cache
            if (!forceRefresh && _cachedServers != null && DateTime.UtcNow < _cacheExpiry)
            {
                _logger.LogInformation($"Returning {_cachedServers.Count} servers from cache");
                return Ok(_cachedServers);
            }

            // Fetch from Edgegap
            var servers = await _edgegapService.GetActiveServersAsync();

            // Update cache
            _cachedServers = servers;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

            _logger.LogInformation($"Returning {servers.Count} servers (cache updated)");

            return Ok(servers);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Edgegap API communication error");
            return Problem(
                title: "Edgegap API Error",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetServers");
            return Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while fetching servers",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// GET /api/servers/{serverId} - Get details for a specific server.
    /// </summary>
    /// <param name="serverId">Edgegap deployment request ID</param>
    /// <returns>Server details</returns>
    [HttpGet("{serverId}")]
    [ProducesResponseType(typeof(ServerInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServerInfo>> GetServer(string serverId)
    {
        try
        {
            _logger.LogInformation($"GET /api/servers/{serverId}");

            // Fetch all servers (uses cache if available)
            var servers = await _edgegapService.GetActiveServersAsync();

            // Find specific server
            var server = servers.FirstOrDefault(s => s.ServerId == serverId);

            if (server == null)
            {
                _logger.LogWarning($"Server {serverId} not found");
                return NotFound(new { message = $"Server {serverId} not found" });
            }

            return Ok(server);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching server {serverId}");
            return Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while fetching server details",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    /// <summary>
    /// GET /api/servers/best - Get the best server based on health, players, and ping.
    /// </summary>
    /// <returns>Best available server</returns>
    [HttpGet("best")]
    [ProducesResponseType(typeof(ServerInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServerInfo>> GetBestServer()
    {
        try
        {
            _logger.LogInformation("GET /api/servers/best");

            var servers = await _edgegapService.GetActiveServersAsync();

            // Get first server (already sorted by health, players, ping)
            var bestServer = servers.FirstOrDefault(s => s.IsHealthy);

            if (bestServer == null)
            {
                _logger.LogWarning("No healthy servers available");
                return NotFound(new { message = "No healthy servers available" });
            }

            _logger.LogInformation($"Best server: {bestServer.City} ({bestServer.CurrentPlayers} players, {bestServer.PingMs}ms)");

            return Ok(bestServer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching best server");
            return Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while finding best server",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
