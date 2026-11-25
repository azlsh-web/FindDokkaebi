using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// LobbyManager : 로비 씬 (시작 화면 + 챕터 선택)
// ─────────────────────────────────────────────────────────────
// 시작 화면 : 아무 데나 누르면 챕터 선택으로.
// 챕터 선택 : 줄에 매달린 사진 5장. 앞 챕터를 깨야 다음 챕터가 열려요 (Progress.IsOpen).
//             열린 챕터를 누르면 큰 사진 창이 뜨고, 창을 누르면 게임 시작.
//             아직 못 연 챕터는 흑백이고, 누르면 흔들리기만 해요. 오른쪽 위 톱니는 소리 설정.
// 칸은 LobbySceneBuilder 가 씬을 조립할 때 채워요.
public class LobbyManager : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] GameObject startPanel;
    [SerializeField] GameObject chapterPanel;
    [SerializeField] GameObject popup;
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Image background;
    [SerializeField] Sprite startBackground;
    [SerializeField] Sprite roomBackground;
    [SerializeField] TMP_Text startText;            // 살살 커졌다 작아지는 "게임 시작하기"

    [Header("챕터 사진")]
    [SerializeField] SimpleAnim[] cardAnims;        // 5장 (잠긴 것은 누르면 흔들려요)
    [SerializeField] Sprite[] chapterPhotos;        // 5장
    [SerializeField] Image[] cardPictures;          // 줄에 매달린 사진 5장 (못 연 챕터는 흑백)
    [SerializeField] Material grayMaterial;         // 흑백으로 그리는 재질 (FindDokkaebi/UI Grayscale)
    [SerializeField] Color chapter5Tint = Color.white;  // 5챕터 그림이 아직 없으면 어둡게
    [SerializeField] Image popupPhoto;
    [SerializeField] TMP_Text popupName;

    [Header("소리 설정")]
    [SerializeField] Slider bgmSlider;
    [SerializeField] Slider sfxSlider;

    static readonly string[] Names = { "소풍 준비", "비 오는 날 방 정리", "동생 생일 파티 준비", "도깨비 간식 바구니", "도깨비의 저주" };

    int popupChapter;
    float lastSfxSample;

    void Start()
    {
        Time.timeScale = 1f;
        Sfx.Bgm(Sfx.BgmRoom);
        Sfx.Ambient(null);          // 2챕터 빗소리를 끄고 와요
        popup.SetActive(false);
        settingsPanel.SetActive(false);
        if (ChapterSelect.OpenChaptersOnLobby)
        {
            ChapterSelect.OpenChaptersOnLobby = false;
            ShowChapters();
        }
        else ShowStart();
    }

    void Update()
    {
        if (startText != null && startPanel.activeSelf)
            startText.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    void ShowStart()
    {
        startPanel.SetActive(true);
        chapterPanel.SetActive(false);
        if (background != null && startBackground != null) background.sprite = startBackground;
    }

    void ShowChapters()
    {
        startPanel.SetActive(false);
        chapterPanel.SetActive(true);
        RefreshCards();
        if (background != null && roomBackground != null) background.sprite = roomBackground;
    }

    /// <summary>열린 챕터는 컬러, 아직 못 연 챕터는 흑백</summary>
    void RefreshCards()
    {
        if (cardPictures == null) return;
        for (int i = 0; i < cardPictures.Length; i++)
            if (cardPictures[i] != null) cardPictures[i].material = Progress.IsOpen(i + 1) ? null : grayMaterial;
    }

    /// <summary>[버튼] 시작 화면 어디든 누르기</summary>
    public void StartTap()
    {
        Sfx.Play(Sfx.Tap);
        ShowChapters();
    }

    /// <summary>[버튼] 챕터 사진 누르기 (index = 0 ~ 4)</summary>
    public void ChapterTap(int index)
    {
        int chapter = index + 1;
        if (!Progress.IsOpen(chapter))
        {
            if (cardAnims != null && index < cardAnims.Length && cardAnims[index] != null)
                cardAnims[index].Play(SimpleAnim.Kind.Shake);
            return;
        }
        Sfx.Play(Sfx.Tap);
        popupChapter = chapter;
        if (chapterPhotos != null && index < chapterPhotos.Length) popupPhoto.sprite = chapterPhotos[index];
        popupPhoto.color = chapter == 5 ? chapter5Tint : Color.white;
        popupName.text = Names[index];
        popup.SetActive(true);
    }

    /// <summary>[버튼] 큰 사진 창 누르기 : 게임 시작</summary>
    public void PopupPlay()
    {
        Sfx.Play(Sfx.Tap);
        ChapterSelect.PlayChapter(popupChapter);
    }

    /// <summary>[버튼] 창 바깥 누르기 : 닫기</summary>
    public void ClosePopup()
    {
        if (!popup.activeSelf) return;
        Sfx.Play(Sfx.Tap);
        popup.SetActive(false);
    }

    /// <summary>[버튼] 오른쪽 위 톱니</summary>
    public void ToggleSettings()
    {
        Sfx.Play(Sfx.Tap);
        bool open = !settingsPanel.activeSelf;
        if (open)
        {
            bgmSlider.SetValueWithoutNotify(Sfx.BgmVolume);
            sfxSlider.SetValueWithoutNotify(Sfx.SfxVolume);
        }
        settingsPanel.SetActive(open);
        Time.timeScale = 1f;        // 로비에서는 멈출 게 없어요
    }

    /// <summary>[버튼] 설정 창의 닫기 · 창 바깥 누르기</summary>
    public void CloseSettings()
    {
        if (!settingsPanel.activeSelf) return;
        Sfx.Play(Sfx.Tap);
        settingsPanel.SetActive(false);
    }

    /// <summary>[슬라이더] 배경음 볼륨 (저장돼요)</summary>
    public void SetBgmVolume(float volume) => Sfx.BgmVolume = volume;

    /// <summary>[슬라이더] 효과음 볼륨 (저장돼요). 움직이면 톡 소리를 들려줘요</summary>
    public void SetSfxVolume(float volume)
    {
        Sfx.SfxVolume = volume;
        if (Time.unscaledTime - lastSfxSample < 0.15f) return;
        lastSfxSample = Time.unscaledTime;
        Sfx.Play(Sfx.Tap);
    }
}
