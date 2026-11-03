// ─────────────────────────────────────────────────────────────
// 찾아라! 도깨비 : AI 판정 중계 서버 (Cloudflare Worker)
// ─────────────────────────────────────────────────────────────
// 왜 필요해요?
//   웹 게임 안에 AI 키를 넣으면 누구나 꺼내 갈 수 있어요.
//   그래서 키는 이 서버(비밀값)에만 두고, 게임은 그림만 보내요.
//
// 어떤 AI 를 쓰나 (비밀값이 있는 것)
//   GEMINI_API_KEY → Google Gemini (무료 등급, 모델 = GEMINI_MODEL)   ← 지금 쓰는 것
//   OPENAI_API_KEY → OpenAI Responses API (유료, 모델 = MODEL)
//
// 게임 → 서버 : POST /judge  { "stage": 1~5, "image": "data:image/png;base64,..." }
// 서버 → AI : 그 단계의 판정 프롬프트(stages.json) + 그림
// 서버 → 게임 : { "ok": true, "text": "<AI 가 쓴 판정 JSON>" }  또는  { "ok": false, "error": "..." }
//
// 다른 용도로 못 쓰게 막는 것들
//   - 프롬프트는 서버가 정해요 (stages.json : Unity 의 StageData 에서 내보낸 것). 게임은 단계 번호와 그림만 보내요.
//   - 허락한 주소(ALLOWED_ORIGINS)의 웹 게임에서 온 요청만 받아요.
//   - 그림 크기 제한, 같은 사람(IP)이 1분에 너무 많이 보내면 거절 (JUDGE_LIMIT).
import stages from "./stages.json";
import appendixB from "./appendix_b.json";   // 기획서 부록 B 의 검증용 프롬프트 원문 (기획서 8장 테스트를 그대로 다시 할 때)

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
    const provider = env.GEMINI_API_KEY ? "gemini" : env.OPENAI_API_KEY ? "openai" : "";
    if (url.pathname === "/health")
      return reply(200, { ok: true, stages: Object.keys(stages).length, key: !!provider, provider, model: provider === "gemini" ? geminiModel(env) : env.MODEL });
    // (관리용) Gemini 에서 쓸 수 있는 모델 이름 보기 — 키는 밖으로 안 나가요
    if (url.pathname === "/models" && allowed && env.GEMINI_API_KEY) {
      const r = await fetch("https://generativelanguage.googleapis.com/v1beta/models?pageSize=200", { headers: { "x-goog-api-key": env.GEMINI_API_KEY } });
      const j = await r.json().catch(() => ({}));
      return reply(r.status, { ok: r.ok, models: (j.models || []).filter(m => (m.supportedGenerationMethods || []).includes("generateContent")).map(m => m.name) });
    }
    if (url.pathname !== "/judge" || request.method !== "POST") return reply(404, { ok: false, error: "없는 주소예요" });
    if (!allowed) return reply(403, { ok: false, error: "허락하지 않은 곳에서 온 요청이에요" });
    if (!provider) return reply(500, { ok: false, error: "서버에 AI 키가 없어요" });

    // 같은 IP 가 1분에 너무 많이 보내면 거절 (wrangler.toml 의 ratelimits)
    if (env.JUDGE_LIMIT) {
      const ip = request.headers.get("CF-Connecting-IP") || "unknown";
      const { success } = await env.JUDGE_LIMIT.limit({ key: ip });
      if (!success) return reply(429, { ok: false, error: "잠깐 너무 많이 보냈어요. 조금 뒤에 다시 해 주세요" });
    }

    let body;
    try { body = await request.json(); } catch { return reply(400, { ok: false, error: "JSON 이 아니에요" }); }
    const prompt = stages[String(body.stage)] ?? appendixB[String(body.stage)];
    if (!prompt) return reply(400, { ok: false, error: "없는 단계예요" });
    const image = body.image;
    if (typeof image !== "string" || !image.startsWith("data:image/png;base64,") || image.length > MAX_IMAGE_CHARS)
      return reply(400, { ok: false, error: "그림 형식이 맞지 않아요" });

    if (provider === "gemini") return reply(...await askGemini(env, prompt, image));

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

function geminiModel(env) { return env.GEMINI_MODEL || "gemini-3.5-flash"; }

// Google Gemini : 글(프롬프트) + 그림 → JSON 한 개 (온도 0 = 같은 그림이면 같은 판정에 가깝게)
async function askGemini(env, prompt, image) {
  const model = geminiModel(env);
  const data = image.slice("data:image/png;base64,".length);
  // thinkingLevel : AI 가 답하기 전에 얼마나 생각할지 (low = 빠름, high = 느리지만 꼼꼼). 이 설정이 없는 모델이면 빼고 다시 보내요.
  const level = env.GEMINI_THINKING || "low";
  let r, raw;
  let useThinking = true;
  // 무료 등급은 사람이 몰리면 "high demand"(503)·"너무 많이 보냄"(429)으로 잠깐 거절해요.
  //   이런 일시적 거절이면 조금 기다렸다가 같은 모델에 다시 물어요 (1.5초 → 3초 → 6초).
  //   20초가 지났으면 더 묻지 않아요 → 게임이 기다리는 60초 안에 꼭 답이 가요.
  const retryDelays = [1500, 3000, 6000];
  const started = Date.now();
  for (let attempt = 0; ; attempt++) {
    const generationConfig = { responseMimeType: "application/json", temperature: 0 };
    if (useThinking) generationConfig.thinkingConfig = { thinkingLevel: level };
    r = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "x-goog-api-key": env.GEMINI_API_KEY },
      body: JSON.stringify({
        contents: [{ role: "user", parts: [{ text: prompt }, { inline_data: { mime_type: "image/png", data } }] }],
        generationConfig,
      }),
    });
    raw = await r.json().catch(() => null);
    // thinkingLevel 설정이 없는 모델이면 빼고 바로 다시 보내요
    if (useThinking && r.status === 400 && /thinking/i.test(raw?.error?.message ?? "")) { useThinking = false; attempt--; continue; }
    const busy = r.status === 429 || r.status === 500 || r.status === 503 || r.status === 504;
    if (!busy || attempt >= retryDelays.length || Date.now() - started > 20000) break;
    await new Promise(resolve => setTimeout(resolve, retryDelays[attempt]));
  }
  if (r.status === 429 || r.status === 503) return [502, { ok: false,
    error: `AI 가 지금 붐벼요. 잠시 뒤 다시 제출해 주세요 (${model} ${r.status})` }];
  if (!r.ok) return [502, { ok: false,
    error: `AI 오류 (${model} ${r.status}) ${raw?.error?.message ?? ""}`.trim().slice(0, 300) }];
  const text = (raw?.candidates?.[0]?.content?.parts ?? []).map(p => p.text || "").join("").trim();
  if (!text) return [502, { ok: false, error: "AI 답변이 비어 있어요 " + (raw?.candidates?.[0]?.finishReason ?? raw?.promptFeedback?.blockReason ?? "") }];
  return [200, { ok: true, text, model }];
}

// ALLOWED_ORIGINS (쉼표로 구분) + 내 컴퓨터에서 테스트할 때(localhost)
function allowedOrigin(origin, env) {
  if (!origin) return false;
  const list = (env.ALLOWED_ORIGINS || "").split(",").map(s => s.trim()).filter(Boolean);
  if (list.includes(origin)) return true;
  return /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(origin);
}
