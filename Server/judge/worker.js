// ─────────────────────────────────────────────────────────────
// 찾아라! 도깨비 : AI 판정 중계 서버 (Cloudflare Worker)
// ─────────────────────────────────────────────────────────────
// 왜 필요해요?
//   웹 게임 안에 OpenAI 키를 넣으면 누구나 꺼내 갈 수 있어요.
//   그래서 키는 이 서버(비밀값 OPENAI_API_KEY)에만 두고, 게임은 그림만 보내요.
//
// 게임 → 서버 : POST /judge  { "stage": 1~5, "image": "data:image/png;base64,..." }
// 서버 → OpenAI : 그 단계의 판정 프롬프트(stages.json) + 그림 → Responses API
// 서버 → 게임 : { "ok": true, "text": "<AI 가 쓴 판정 JSON>" }  또는  { "ok": false, "error": "..." }
//
// 다른 용도로 못 쓰게 막는 것들
//   - 프롬프트는 서버가 정해요 (stages.json : Unity 의 StageData 에서 내보낸 것). 게임은 단계 번호와 그림만 보내요.
//   - 허락한 주소(ALLOWED_ORIGINS)의 웹 게임에서 온 요청만 받아요.
//   - 그림 크기 제한, 같은 사람(IP)이 1분에 너무 많이 보내면 거절 (JUDGE_LIMIT).
import stages from "./stages.json";

const MAX_IMAGE_CHARS = 1_500_000;   // 512x512 PNG 는 보통 수십 KB 라 넉넉해요

export default {
  async fetch(request, env) {
    const origin = request.headers.get("Origin") || "";
    // 웹 게임은 브라우저가 Origin 을 붙여요. Unity 에디터 · PC 빌드는 Origin 이 없어서 머리글로 알려요.
    const allowed = allowedOrigin(origin, env) || (!origin && request.headers.get("X-FindDokkaebi-Client") === "unity");
    const cors = {
      "Access-Control-Allow-Origin": allowed ? origin : "null",
      "Access-Control-Allow-Methods": "POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type, X-FindDokkaebi-Client",
      "Vary": "Origin",
    };
    const reply = (status, obj) =>
      new Response(JSON.stringify(obj), { status, headers: { ...cors, "Content-Type": "application/json; charset=utf-8" } });

    if (request.method === "OPTIONS") return new Response(null, { status: allowed ? 204 : 403, headers: cors });
    const url = new URL(request.url);
    if (url.pathname === "/health") return reply(200, { ok: true, stages: Object.keys(stages).length, key: !!env.OPENAI_API_KEY });
    if (url.pathname !== "/judge" || request.method !== "POST") return reply(404, { ok: false, error: "없는 주소예요" });
    if (!allowed) return reply(403, { ok: false, error: "허락하지 않은 곳에서 온 요청이에요" });
    if (!env.OPENAI_API_KEY) return reply(500, { ok: false, error: "서버에 OpenAI 키가 없어요" });

    // 같은 IP 가 1분에 너무 많이 보내면 거절 (wrangler.toml 의 ratelimits)
    if (env.JUDGE_LIMIT) {
      const ip = request.headers.get("CF-Connecting-IP") || "unknown";
      const { success } = await env.JUDGE_LIMIT.limit({ key: ip });
      if (!success) return reply(429, { ok: false, error: "잠깐 너무 많이 보냈어요. 조금 뒤에 다시 해 주세요" });
    }

    let body;
    try { body = await request.json(); } catch { return reply(400, { ok: false, error: "JSON 이 아니에요" }); }
    const prompt = stages[String(body.stage)];
    if (!prompt) return reply(400, { ok: false, error: "없는 단계예요" });
    const image = body.image;
    if (typeof image !== "string" || !image.startsWith("data:image/png;base64,") || image.length > MAX_IMAGE_CHARS)
      return reply(400, { ok: false, error: "그림 형식이 맞지 않아요" });

    // OpenAI Responses API (Unity 의 OpenAIJudge 와 같은 요청 모양)
    const ai = await fetch("https://api.openai.com/v1/responses", {
      method: "POST",
      headers: { "Content-Type": "application/json", "Authorization": "Bearer " + env.OPENAI_API_KEY },
      body: JSON.stringify({
        model: env.MODEL || "gpt-6-luna",
        input: [{ role: "user", content: [{ type: "input_text", text: prompt }, { type: "input_image", image_url: image }] }],
        text: { format: { type: "json_object" } },
      }),
    });
    const raw = await ai.json().catch(() => null);
    if (!ai.ok) return reply(502, { ok: false, error: `AI 오류 (${ai.status}) ${raw?.error?.message ?? ""}`.trim() });

    // 응답에서 AI 가 쓴 글(output_text)만 꺼내서 돌려줘요
    for (const o of raw?.output ?? []) {
      if (o.type !== "message") continue;
      for (const c of o.content ?? []) if (c.type === "output_text" && c.text) return reply(200, { ok: true, text: c.text });
    }
    return reply(502, { ok: false, error: "AI 답변이 비어 있어요" });
  },
};

// ALLOWED_ORIGINS (쉼표로 구분) + 내 컴퓨터에서 테스트할 때(localhost)
function allowedOrigin(origin, env) {
  if (!origin) return false;
  const list = (env.ALLOWED_ORIGINS || "").split(",").map(s => s.trim()).filter(Boolean);
  if (list.includes(origin)) return true;
  return /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(origin);
}
