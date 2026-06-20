// Netlify Function: live world-server population for the Fathoms Deep launcher.
// Queries PlayFab Multiplayer Servers server-side so the launcher never holds the secret key.
//
// Set these in Netlify > Site settings > Environment variables:
//   PLAYFAB_TITLE_ID   (e.g. A8DA4)
//   PLAYFAB_SECRET_KEY (the title secret key)
//   MPS_BUILD_ID       (e.g. 14982ad5-cd85-4270-89c5-11945bc3de17)
//   MPS_REGION         (optional, default EastUs)
//
// Returns: { online: bool, players: int, active: int, standby: int, servers: int }

const TITLE  = process.env.PLAYFAB_TITLE_ID;
const SECRET = process.env.PLAYFAB_SECRET_KEY;
const BUILD  = process.env.MPS_BUILD_ID;
const REGION = process.env.MPS_REGION || "EastUs";

exports.handler = async () => {
  if (!TITLE || !SECRET || !BUILD) return reply(200, { online: false, players: 0, error: "not configured" });
  try {
    const base = `https://${TITLE}.playfabapi.com`;
    const tok = await post(`${base}/Authentication/GetEntityToken`, { "X-SecretKey": SECRET }, {});
    const entity = tok && tok.data && tok.data.EntityToken;
    if (!entity) return reply(200, { online: false, players: 0, error: "auth" });

    const list = await post(`${base}/MultiplayerServer/ListMultiplayerServers`,
      { "X-EntityToken": entity }, { BuildId: BUILD, Region: REGION, PageSize: 20 });
    const servers = (list && list.data && list.data.MultiplayerServerSummaries) || [];

    let players = 0, active = 0, standby = 0;
    for (const s of servers) {
      const state = (s.State || "").toLowerCase();
      if (state === "active") { active++; players += (s.ConnectedPlayers || []).length; }
      else if (state === "standingby") { standby++; }
    }
    return reply(200, { online: active > 0, players, active, standby, servers: servers.length });
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
