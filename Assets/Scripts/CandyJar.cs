using UnityEngine;

// ─────────────────────────────────────────────────────────────
// CandyJar : 끝 장면의 사탕 통 (11/23 다시 넣음, C-45 되돌림)
// ─────────────────────────────────────────────────────────────
// - 유리 뒤쪽 → 사탕 → 테두리 · 뚜껑 순서로 겹친 그림이에요 (MvpSceneBuilder.NewCandyJar 가 만들어요).
// - Fill(n) 하면 통 아래부터 사탕 n개가 켜지고, 사탕마다 붙은 Pop 이 조금씩 늦게 톡톡 나타나요.
// - 1~4챕터는 물건마다 남은 사탕을 더한 수, 5챕터 해피 엔딩은 남은 하트 수만큼 담겨요.
public class CandyJar : MonoBehaviour
{
    [SerializeField] GameObject[] candies;

    /// <summary>지금 통에 보이는 사탕 수</summary>
    public int Shown { get; private set; }

    public int Capacity => candies == null ? 0 : candies.Length;

    public void Fill(int count)
    {
        Shown = Mathf.Clamp(count, 0, Capacity);
        for (int i = 0; i < Capacity; i++)
            if (candies[i] != null) candies[i].SetActive(i < Shown);
    }
}
