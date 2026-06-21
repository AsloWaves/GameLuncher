// Netlify Function: live world-server population for the Fathoms Deep launcher.
// Queries PlayFab Multiplayer Servers server-side so the launcher never holds the secret key.
//
// Set these in Netlify > Site settings > Environment variables:
//   PLAYFAB_TITLE_ID   (e.g. A8DA4)        REQUIRED
//   PLAYFAB_SECRET_KEY (the title secret)  REQUIRED
//   MPS_BUILD_ID       (optional)  if unset, the newest MPS build is auto-discovered
//   MPS_BUILD_PREFIX   (optional, default "FD_3D_Server")  which build to pick when auto-discovering
//   MPS_REGION         (optional, default EastUs)
//
// Returns: { online: bool, players: int, active: int, standby: int, servers: int, build: string }

const TITLE   = process.env.PLAYFAB_TITLE_ID;
const SECRET  = process.env.PLAYFAB_SECRET_KEY;
const BUILD   = process.env.MPS_BUILD_ID || null;
const PREFIX  = process.env.MPS_BUILD_PREFIX || "FD_3D_Server";
const REGION  = process.env.MPS_REGION || "EastUs";

exports.handler = async () => {
  if (!TITLE || !SECRET) return reply(200, { online: false, players: 0, error: "not configured" });
  try {
    const base = `https://${TITLE}.playfabapi.com`;
    const tok = await post(`${base}/Authentication/GetEntityToken`, { "X-SecretKey": SECRET }, {});
    const entity = tok && tok.data && tok.data.EntityToken;
    if (!entity) return reply(200, { online: false, players: 0, error: "auth" });

    // Resolve the build: explicit env var, else newest build whose name starts with PREFIX
    // (falls back to the first build). Auto-following survives server redeploys.
    let buildId = BUILD, buildName = BUILD || "";
    if (!buildId) {
      const sums = await post(`${base}/MultiplayerServer/ListBuildSummariesV2`,
        { "X-EntityToken": entity }, { PageSize: 50 });
      const builds = (sums && sums.data && sums.data.BuildSummaries) || [];
      if (!builds.length) return reply(200, { online: false, players: 0, error: "no MPS build" });
      const matches = builds.filter(b => (b.BuildName || "").startsWith(PREFIX));
      const pick = (matches.length ? matches : builds)
        .sort((a, b) => String(b.BuildName).localeCompare(String(a.BuildName)))[0];
      buildId = pick.BuildId; buildName = pick.BuildName;
    }

    const list = await post(`${base}/MultiplayerServer/ListMultiplayerServers`,
      { "X-EntityToken": entity }, { BuildId: buildId, Region: REGION, PageSize: 20 });
    const servers = (list && list.data && list.data.MultiplayerServerSummaries) || [];

    let players = 0, active = 0, standby = 0, host = "", port = 0;
    for (const s of servers) {
      const state = (s.State || "").toLowerCase();
      if (state === "active") {
        active++; players += (s.ConnectedPlayers || []).length;
        if (!host) {
          host = s.IPV4Address || s.FQDN || "";
          const ports = s.Ports || [];
          const gp = ports.find(p => p.Name === "game_port") || ports[0];
          if (gp) port = gp.Num || 0;
        }
      } else if (state === "standingby") { standby++; }
    }
    const endpoint = host ? (port ? `${host}:${port}` : host) : "";
    return reply(200, { online: active > 0, players, active, standby, servers: servers.length, build: buildName, host, port, endpoint });
  } catch (e) {
    return reply(200, { online: false, players: 0, error: String(e && e.message ? e.message : e) });
  }
};

async function post(url, headers, body) {
  const r = await fetch(url, {
    method: "POST",
    headers: Object.assign({ "Content-Type": "application/json" }, headers),
    body: JSON.stringify(body),
  });
  return r.json();
}

function reply(statusCode, obj) {
  return {
    statusCode,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
      "Access-Control-Allow-Origin": "*",
    },
    body: JSON.stringify(obj),
  };
}
// redeploy trigger: env vars set 2026-06-21
