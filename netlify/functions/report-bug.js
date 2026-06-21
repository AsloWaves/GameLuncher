// Netlify Function: forward a launcher bug report to a Discord webhook.
// Keeps the webhook server-side (never shipped in the client).
//
// Set in Netlify > Site settings > Environment variables:
//   BUG_REPORT_WEBHOOK = https://discord.com/api/webhooks/....   REQUIRED
//
// Launcher POSTs JSON: { title, description, player, build, launcher, specs }
// Returns: { ok: true } or { ok: false, error }

const WEBHOOK = process.env.BUG_REPORT_WEBHOOK;

exports.handler = async (event) => {
  if (event.httpMethod !== "POST") return reply(405, { ok: false, error: "POST only" });
  if (!WEBHOOK) return reply(200, { ok: false, error: "not configured" });
  try {
    const b = JSON.parse(event.body || "{}");
    const title = (b.title || "Untitled report").toString().slice(0, 240);
    const desc = (b.description || "(no description)").toString().slice(0, 3500);

    const fields = [];
    if (b.player)   fields.push({ name: "Player",   value: String(b.player).slice(0, 200),   inline: true });
    if (b.build)    fields.push({ name: "Client",   value: String(b.build).slice(0, 100),     inline: true });
    if (b.launcher) fields.push({ name: "Launcher", value: String(b.launcher).slice(0, 100),  inline: true });
    if (b.specs)    fields.push({ name: "System",   value: String(b.specs).slice(0, 900),      inline: false });

    const payload = {
      username: "Fathoms Deep — Bug Reports",
      embeds: [{
        title: "🐛 " + title,
        description: desc,
        color: 0xE03A4A,
        fields,
        timestamp: new Date().toISOString(),
      }],
    };

    const r = await fetch(WEBHOOK, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    if (!r.ok) return reply(200, { ok: false, error: "discord " + r.status });
    return reply(200, { ok: true });
  } catch (e) {
    return reply(200, { ok: false, error: String(e && e.message ? e.message : e) });
  }
};

function reply(statusCode, obj) {
  return {
    statusCode,
    headers: {
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Headers": "Content-Type",
      "Access-Control-Allow-Methods": "POST, OPTIONS",
    },
    body: JSON.stringify(obj),
  };
}
