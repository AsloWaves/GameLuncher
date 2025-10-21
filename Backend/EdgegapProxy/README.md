# Edgegap Proxy Backend API

ASP.NET Core backend that securely proxies Edgegap API calls for Unity game clients.

**Security**: Keeps Edgegap API token server-side (never exposed to clients)

---

## Features

✅ Fetch active Edgegap deployments securely
✅ Validate server health via HTTP endpoint
✅ Filter by tags (e.g., "production")
✅ Sort by health, player count, and ping
✅ 30-second caching to reduce API calls
✅ CORS enabled for Unity WebGL
✅ Swagger API documentation

---

## API Endpoints

### GET /api/servers
List all active game servers with health status.

**Query Parameters:**
- `forceRefresh` (optional): Bypass cache and force refresh from Edgegap

**Response:**
```json
[
  {
    "serverId": "abc123",
    "serverName": "Chicago Server",
    "ipAddress": "172.234.217.194",
    "port": 32479,
    "region": "United States",
    "city": "Chicago",
    "country": "United States",
    "currentPlayers": 5,
    "maxPlayers": 300,
    "pingMs": 45,
    "isHealthy": true,
    "status": "Ready",
    "tags": ["production"]
  }
]
```

### GET /api/servers/{serverId}
Get details for a specific server.

### GET /api/servers/best
Get the best server based on health, player count, and ping.

### GET /health
Health check endpoint for the backend API itself.

---

## Setup Instructions

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Edgegap API token (from dashboard)

### Local Development

1. **Clone and Navigate:**
   ```bash
   cd Backend/EdgegapProxy
   ```

2. **Configure API Token:**

   Edit `appsettings.Development.json` and replace placeholder:
   ```json
   {
     "Edgegap": {
       "ApiToken": "YOUR_ACTUAL_API_TOKEN_HERE"
     }
   }
   ```

   **IMPORTANT:** This file is in `.gitignore` to protect your token!

3. **Restore Dependencies:**
   ```bash
   dotnet restore
   ```

4. **Run Backend:**
   ```bash
   dotnet run
   ```

5. **Test API:**
   - Swagger UI: `http://localhost:5000/swagger`
   - Health check: `http://localhost:5000/health`
   - Server list: `http://localhost:5000/api/servers`

---

## Deployment Options

### Option 1: Azure App Service (Recommended for C#)

**Why Azure:**
- Native ASP.NET Core support
- Free tier available ($0/month for 60 CPU minutes/day)
- Easy deployment from Visual Studio
- Automatic HTTPS

**Setup:**
1. Create free Azure account: https://azure.microsoft.com/free
2. Install Azure CLI: https://aka.ms/installazurecli
3. Deploy:
   ```bash
   # Login
   az login

   # Create resource group
   az group create --name wos-backend --location eastus

   # Create app service plan (Free tier)
   az appservice plan create --name wos-plan --resource-group wos-backend --sku F1

   # Create web app
   az webapp create --name wos-edgegap-proxy --resource-group wos-backend --plan wos-plan --runtime "DOTNET|8.0"

   # Set API token (secure environment variable)
   az webapp config appsettings set --name wos-edgegap-proxy --resource-group wos-backend --settings Edgegap__ApiToken="YOUR_TOKEN"

   # Deploy
   dotnet publish -c Release
   az webapp deployment source config-zip --resource-group wos-backend --name wos-edgegap-proxy --src ./bin/Release/net8.0/publish.zip
   ```

4. **Your backend URL:** `https://wos-edgegap-proxy.azurewebsites.net`

**Pros:**
- ✅ Free tier sufficient for development
- ✅ Native C# support
- ✅ Automatic HTTPS
- ✅ Scales to production easily

**Cons:**
- ❌ Requires Azure account
- ❌ Free tier has 60 CPU minutes/day limit

---

### Option 2: Render.com (Easiest - Free Tier)

**Why Render:**
- Free tier: 750 hours/month (enough for development)
- Deploy from GitHub in 5 minutes
- No credit card required
- Docker support for .NET apps

**Setup:**

1. **Prepare Repository:**

   Ensure `Backend/EdgegapProxy/Dockerfile` exists (should be present in repo):
   ```dockerfile
   # This Dockerfile is already created in the repo
   # It builds the .NET app and configures PORT binding for Render
   ```

2. **Sign Up:**

   Create account at https://render.com (free with GitHub)

3. **Create Web Service:**

   - Click "New" → "Web Service"
   - Connect your GitHub repository
   - Configure deployment:

4. **Deployment Settings:**

   ```
   Name: wos-edgegap-proxy
   Region: Oregon (US West) - closest to your servers
   Branch: main

   Root Directory: Backend/EdgegapProxy

   Runtime: Docker (IMPORTANT: Select Docker, NOT .NET)

   Dockerfile Path: Dockerfile (relative to Root Directory)

   Build Command: (leave BLANK - Render uses Dockerfile)
   Start Command: (leave BLANK - uses ENTRYPOINT from Dockerfile)

   Instance Type: Free
   ```

5. **Environment Variables:**

   Add environment variable in Render dashboard:
   - Key: `Edgegap__ApiToken`
   - Value: `YOUR_EDGEGAP_API_TOKEN`

6. **Deploy:**

   Click "Create Web Service" - Render will:
   - Build Docker image (~2-3 minutes)
   - Deploy container
   - Provide URL: `https://wos-edgegap-proxy.onrender.com`

7. **Test Deployment:**
   ```bash
   # Health check (wait ~50 seconds for cold start on first request)
   curl https://wos-edgegap-proxy.onrender.com/health

   # Get server list
   curl https://wos-edgegap-proxy.onrender.com/api/servers
   ```

**Important Notes:**

⚠️ **Cold Starts:** Free tier sleeps after 15 minutes of inactivity. First request after sleep takes ~50 seconds to 1 minute to wake up. Subsequent requests are instant.

⚠️ **PORT Binding:** The Dockerfile is configured to bind to `0.0.0.0:$PORT` (not localhost). Render provides `PORT` environment variable (default 10000). DO NOT change this.

⚠️ **Free Tier Limits:** 750 hours/month is sufficient for development. For production 24/7 uptime, upgrade to paid tier ($7/month).

**Pros:**
- ✅ True free tier (750 hours/month)
- ✅ No credit card required
- ✅ Docker support for .NET
- ✅ Auto-deploy from GitHub commits
- ✅ Automatic HTTPS
- ✅ Easy environment variable management

**Cons:**
- ❌ Cold starts after 15 minutes inactivity (~50s wake time)
- ❌ 750 hours/month limit (good for development, insufficient for 24/7 production)

---

### Option 3: Your Own VPS (Full Control)

**If you have VPS with Ubuntu/Debian:**

```bash
# Install .NET 8.0
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Clone and build
cd /opt
git clone YOUR_REPO
cd YOUR_REPO/Backend/EdgegapProxy
dotnet publish -c Release -o /opt/edgegap-proxy

# Set API token as environment variable
export Edgegap__ApiToken="YOUR_TOKEN"

# Run as systemd service
sudo nano /etc/systemd/system/edgegap-proxy.service
```

**Service file:**
```ini
[Unit]
Description=Edgegap Proxy API
After=network.target

[Service]
WorkingDirectory=/opt/edgegap-proxy
ExecStart=/root/.dotnet/dotnet /opt/edgegap-proxy/EdgegapProxy.dll
Restart=always
Environment=Edgegap__ApiToken=YOUR_TOKEN
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable edgegap-proxy
sudo systemctl start edgegap-proxy
```

**Pros:**
- ✅ Full control
- ✅ No limits
- ✅ Can use existing infrastructure

**Cons:**
- ❌ Requires VPS and Linux knowledge
- ❌ Manual SSL setup with Let's Encrypt

---

### Option 4: Heroku (Alternative)

**Setup:**
1. Install Heroku CLI: https://devcenter.heroku.com/articles/heroku-cli
2. Deploy:
   ```bash
   cd Backend/EdgegapProxy
   heroku login
   heroku create wos-edgegap-proxy
   heroku buildpacks:set jincod/dotnetcore
   heroku config:set Edgegap__ApiToken="YOUR_TOKEN"
   git push heroku main
   ```

**Pros:**
- ✅ Free tier available
- ✅ Simple deployment

**Cons:**
- ❌ Requires credit card (even for free tier)
- ❌ Slow cold starts (dyno sleeps after 30 mins inactivity)

---

## Hosting Comparison for Your Setup

| Platform | Setup Time | Free Tier | Best For | Recommended? |
|----------|------------|-----------|----------|--------------|
| **Render** | 5 mins | 750 hrs/month | Dev/testing with cold starts OK | ✅ Yes - START HERE |
| **Azure** | 10 mins | 60 CPU mins/day | C# developers, production | ✅ Yes - for production |
| **VPS** | 30 mins | Depends | Full control, 24/7 uptime | ⚠️ Only if you have VPS |
| **Heroku** | 15 mins | Limited | Quick prototypes | ⚠️ Slow cold starts |

**My Recommendation:**
1. **Development/Testing:** Use **Render** (easy Docker deployment, true free tier, no credit card)
2. **Production:** Migrate to **Azure App Service B1** ($13/month, 99.95% SLA, no cold starts) or keep Render paid tier ($7/month)

---

## Configuration

Edit `appsettings.json` to customize:

```json
{
  "Edgegap": {
    "ApiBaseUrl": "https://api.edgegap.com/v1",
    "HealthCheckPort": 8080,
    "HealthCheckTimeout": 5,
    "RequiredTags": ["production"]
  }
}
```

**Environment Variables (Production):**
- `Edgegap__ApiToken` - Your Edgegap API token (REQUIRED)
- `ASPNETCORE_ENVIRONMENT` - Set to "Production" for prod deployment

---

## Security Best Practices

✅ **DO:**
- Store API token in environment variables (never commit to git)
- Use HTTPS in production (Railway/Azure/Heroku provide this automatically)
- Monitor backend logs for suspicious activity
- Use `appsettings.Production.json` for production-specific settings

❌ **DON'T:**
- Commit `appsettings.Development.json` with real token (already in .gitignore)
- Expose backend to public internet without authentication (for production, add API keys)
- Log API tokens (already handled)

---

## Testing

**Manual Test:**
```bash
# Get server list
curl http://localhost:5000/api/servers

# Get best server
curl http://localhost:5000/api/servers/best

# Health check
curl http://localhost:5000/health
```

**From Unity:**
```csharp
string backendUrl = "https://your-backend-url.com";
UnityWebRequest request = UnityWebRequest.Get($"{backendUrl}/api/servers");
yield return request.SendWebRequest();
// Parse JSON response...
```

---

## Troubleshooting

### Error: "Edgegap API token not configured"
**Fix:** Set `Edgegap__ApiToken` in environment variables or `appsettings.Development.json`

### Error: Port 5000 already in use (Local Development)
**Fix:** Change port with `dotnet run --urls="http://localhost:5001"`

### Error: 502 Bad Gateway (Edgegap API Error)
**Fix:**
1. Check API token is correct
2. Verify Edgegap API is accessible: `curl https://api.edgegap.com/v1`
3. Check backend logs: `dotnet run --verbosity detailed`

### No servers returned
**Fix:**
1. Check deployments exist in Edgegap dashboard
2. Verify deployments have required tags (e.g., "production")
3. Check health endpoint is accessible from backend (port 8080 exposed in Edgegap)

### Render.com Deployment Issues

#### Error: "Build failed - .NET runtime not found"
**Root Cause:** Selected wrong runtime in Render dashboard

**Fix:** Runtime MUST be set to "Docker" (not .NET, Node.js, or any other option)
1. Go to Render dashboard → Your service → Settings
2. Change "Runtime" to "Docker"
3. Redeploy

#### Error: "503 Service Unavailable" on first request after inactivity
**Root Cause:** Free tier cold start (expected behavior)

**Fix:** This is normal for Render free tier:
- Wait 50 seconds to 1 minute for container to wake up
- Subsequent requests will be instant
- For production: Upgrade to paid tier ($7/month) to prevent sleeping

#### Error: "Application failed to respond" or "Port binding error"
**Root Cause:** App not binding to `0.0.0.0:$PORT`

**Fix:** Ensure Dockerfile has correct configuration:
```dockerfile
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}
```

**DO NOT:**
- ❌ Bind to localhost (won't accept external connections)
- ❌ Hardcode port 5000 or 8080 (Render uses $PORT variable, usually 10000)
- ❌ Change PORT environment variable in Render dashboard

#### Error: "Environment variable not found"
**Root Cause:** `Edgegap__ApiToken` not set in Render dashboard

**Fix:**
1. Go to Render dashboard → Your service → Environment
2. Add variable:
   - Key: `Edgegap__ApiToken` (exact spelling, two underscores)
   - Value: Your Edgegap API token
3. Save and redeploy

#### Build takes too long (>10 minutes)
**Root Cause:** Multi-stage Docker build downloads large .NET SDK

**Fix:** This is expected for first build (~3-5 minutes). Render caches layers for subsequent builds (~1-2 minutes).

---

## Next Steps

1. ✅ Deploy backend to Railway or Azure
2. ✅ Test API endpoints in browser/Postman
3. ✅ Update Unity client to call your backend URL
4. ✅ Test full flow: Unity → Backend → Edgegap → Server

---

## Support

**Documentation:**
- ASP.NET Core: https://docs.microsoft.com/aspnet/core
- Edgegap API: https://docs.edgegap.com/api
- Railway: https://docs.railway.app

**Questions?** Check backend logs with `dotnet run` or check deployment logs in hosting platform.
