using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────
// IncenseBurn : 그리는 동안 향이 진짜처럼 타들어 가는 연출
// ─────────────────────────────
//   - 줄기가 위에서부터 짧아지고, 불씨 · 불꽃이 끝을 따라 내려와요.
//   - 타고 남은 재가 끝에 쌓이다가 뚝 떨어지고, 연기가 흔들리며 올라가요.
//   - 10초 남으면 불꽃이 커지고 남은 초가 빨갛게 두근거려요.
// GameManager 가 매 프레임 Show 로 남은 비율을 알려 줘요.
// horizontal : 맨 위 가로 막대 (그리기 화면 개편 뒤). 오른쪽 끝이 타들어 가며 왼쪽으로 줄어들고, 불꽃 · 연기가 끝을 따라가요.
//   재는 쌓지 않아요. (나중에 도깨비불 게이지 그림이 오면 이 자리를 바꿔요)
public class IncenseBurn : MonoBehaviour
{
    [SerializeField] Image stick;              // 타는 줄기 (아래 기준 Filled)
    [SerializeField] RectTransform tip;        // 줄기 끝을 따라 내려가는 묶음
    [SerializeField] RectTransform flame;      // 불씨 + 불꽃
    [SerializeField] Image glow;               // 불꽃 뒤 빛
    [SerializeField] RectTransform ash;        // 끝에 쌓이는 재
    [SerializeField] Image fallingAsh;         // 떨어지는 재
    [SerializeField] Image[] smoke;            // 연기 조각들
    [SerializeField] TMP_Text secondsText;     // 남은 초
    [SerializeField] bool horizontal;          // 가로 막대 (연기는 tip 의 자식)

    [Tooltip("다 타도 잔 위에 남는 줄기의 비율")]
    [SerializeField] float baseRatio = 0.02f;
    [SerializeField] float urgentSeconds = 10f;
    [SerializeField] float maxAshHeight = 70f;
    [Tooltip("향 전체가 타는 동안 재가 떨어지는 횟수")]
    [SerializeField] int ashDrops = 5;
    [Tooltip("재가 쌓이기 시작하는 높이 (불씨 그림의 윗면)")]
    [SerializeField] float ashBottom = 24f;

    const float SmokeLife = 3.2f;
    const float SmokeRise = 270f;
    const float AshFallTime = 0.7f;
    const float AshFallDistance = 150f;

    Color normalTextColor;
    float flameBaseY;
    float fallTime = -1f;
    float fallStartY;
    int lastAshStep;

    void Awake()
    {
        normalTextColor = secondsText.color;
        flameBaseY = flame.anchoredPosition.y;
    }

    /// <summary>remaining = 남은 비율 (1 → 0), seconds = 남은 초</summary>
    public void Show(float remaining, float seconds)
    {
        remaining = Mathf.Clamp01(remaining);
        stick.fillAmount = Mathf.Lerp(baseRatio, 1f, remaining);
        bool urgent = seconds <= urgentSeconds;
        ShowFlame(urgent);
        if (horizontal)
        {
            tip.anchoredPosition = new Vector2(stick.rectTransform.rect.width * stick.fillAmount, 0f);
            ash.gameObject.SetActive(false);
            fallingAsh.enabled = false;
            ShowSmoke(Vector2.zero, 26f, 95f, 0.5f, urgent);
        }
        else
        {
            float tipY = stick.rectTransform.rect.height * stick.fillAmount;
            tip.anchoredPosition = new Vector2(0f, tipY);
            ShowAsh(1f - remaining, tipY);
            ShowSmoke(new Vector2(0f, tipY), 70f, SmokeRise, 1f, urgent);
        }

        float beat = urgent ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 5f)) : 1f;
        secondsText.color = urgent ? new Color(0.78f, 0.16f, 0.13f) : normalTextColor;
        secondsText.transform.localScale = Vector3.one * beat;
    }

    void ShowFlame(bool urgent)
    {
        float size = urgent ? 1.35f : 1f;
        float flicker = Mathf.PerlinNoise(Time.time * 7f, 0.3f);
        flame.localScale = new Vector3(size * Mathf.Lerp(0.92f, 1.08f, flicker), size * Mathf.Lerp(0.9f, 1.2f, flicker), 1f);
        flame.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-5f, 5f, Mathf.PerlinNoise(Time.time * 4f, 1.7f)));

        Color c = urgent ? new Color(1f, 0.28f, 0.1f) : new Color(1f, 0.55f, 0.15f);
        c.a = Mathf.Lerp(0.45f, 0.85f, flicker);
        glow.color = c;
        glow.rectTransform.localScale = Vector3.one * (urgent ? 1.5f : 1f) * Mathf.Lerp(0.9f, 1.15f, flicker);
    }

    // 재 : 불씨 위에 탄 만큼 쌓이다가 일정하게 뚝 떨어져요. 불꽃은 쌓인 재 꼭대기에 얹혀 같이 오르내려요.
    void ShowAsh(float burned, float tipY)
    {
        float steps = burned * ashDrops;
        int step = Mathf.FloorToInt(steps);
        if (step < lastAshStep) fallTime = -1f;          // 다시 그리기 : 처음부터
        else if (step > lastAshStep)
        {
            fallTime = 0f;                               // 쌓인 재가 떨어지기 시작
            fallStartY = tipY + maxAshHeight * 0.5f;
        }
        lastAshStep = step;

        Vector2 size = ash.sizeDelta;
        size.y = Mathf.Max(4f, maxAshHeight * (steps - step));
        ash.sizeDelta = size;
        ash.anchoredPosition = new Vector2(ash.anchoredPosition.x, ashBottom);
        flame.anchoredPosition = new Vector2(flame.anchoredPosition.x, flameBaseY + size.y);

        bool falling = fallTime >= 0f && fallTime < AshFallTime;
        fallingAsh.enabled = falling;
        if (!falling) return;
        fallTime += Time.deltaTime;
        float p = fallTime / AshFallTime;
        fallingAsh.rectTransform.anchoredPosition = new Vector2(18f * p, fallStartY - AshFallDistance * p * p);
        fallingAsh.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -70f * p);
        Color c = fallingAsh.color;
        c.a = 1f - p * p;
        fallingAsh.color = c;
    }

    // 연기 : 조각마다 시간을 어긋나게 올려 보내고, 위로 갈수록 커지며 옅어져요
    void ShowSmoke(Vector2 origin, float start, float rise, float size, bool urgent)
    {
        for (int i = 0; i < smoke.Length; i++)
        {
            float offset = i / (float)smoke.Length;
            float p = Mathf.Repeat(Time.time / SmokeLife + offset, 1f);
            float sway = Mathf.Sin((p * 3.2f + i * 1.3f)) * Mathf.Lerp(3f, 26f, p);
            RectTransform rt = smoke[i].rectTransform;
            rt.anchoredPosition = origin + new Vector2(sway * size, start + rise * p);
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.1f, p) * size;
            float peak = urgent ? 0.8f : 0.6f;
            smoke[i].color = new Color(0.45f, 0.45f, 0.48f, peak * Mathf.Sin(Mathf.PI * Mathf.Sqrt(p)));
        }
    }
}
