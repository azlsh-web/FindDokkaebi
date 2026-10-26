using UnityEngine;

// ─────────────────────────────────────────────────────────────
// SimpleAnim : 아주 간단한 움직임 2가지 (애니메이터 없이 코드로)
// ─────────────────────────────────────────────────────────────
//   Sway : 좌우로 갸웃갸웃 (판정 중 도깨비가 쪽지를 들여다보며 고민하는 모습)
//   Pop  : 켜질 때 0 → 살짝 크게 → 원래 크기 (도깨비가 물건을 "뿅" 꺼내는 모습)
// 오브젝트에 붙이고 Inspector 에서 kind 만 고르면 돼요.
public class SimpleAnim : MonoBehaviour
{
    public enum Kind { Sway, Pop }

    public Kind kind = Kind.Sway;

    [Tooltip("Sway : 흔들리는 빠르기")]
    public float speed = 3f;

    [Tooltip("Sway : 기울어지는 각도(도)")]
    public float angle = 8f;

    [Tooltip("Pop : 커지는 데 걸리는 시간(초)")]
    public float popTime = 0.45f;

    float t;

    // OnEnable : 오브젝트가 켜질 때마다 실행 (패널이 다시 열릴 때도)
    void OnEnable()
    {
        t = 0f;
        if (kind == Kind.Pop) transform.localScale = Vector3.zero;
    }

    void Update()
    {
        t += Time.deltaTime;

        if (kind == Kind.Sway)
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * speed) * angle);
        }
        else
        {
            // 0 → 1.15 → 1 로 튕기듯 커지기
            float p = Mathf.Clamp01(t / popTime);
            float scale = p < 0.7f ? Mathf.Lerp(0f, 1.15f, p / 0.7f) : Mathf.Lerp(1.15f, 1f, (p - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * scale;
        }
    }
}
