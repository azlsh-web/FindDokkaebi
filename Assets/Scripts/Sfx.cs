using UnityEngine;

// ─────────────────────────────────────────────────────────────
// Sfx : 효과음 · 배경음 재생
// ─────────────────────────────────────────────────────────────
// Assets/Resources/Sound 폴더에 아래 이름의 파일을 넣으면 바로 소리가 나요.
// 파일이 없으면 조용히 넘어가서, 소리가 아직 없어도 게임은 그대로 돌아가요.
// 필요한 소리 목록 : Docs/전달/12_사운드팀_전달사항.pdf
// 볼륨 : 설정 창(오른쪽 위 톱니)에서 배경음 · 효과음을 따로 0 ~ 1 로 조절하고, PlayerPrefs 에 저장해서 다시 켜도 그대로예요.
public static class Sfx
{
    // 효과음
    public const string Tap = "sfx_tap";
    public const string Talk = "sfx_talk";
    public const string Count = "sfx_count";
    public const string SnackIn = "sfx_snack_in";
    public const string Magnifier = "sfx_magnifier";
    public const string Submit = "sfx_submit";
    public const string Give = "sfx_give";
    public const string Item = "sfx_item";
    public const string Success = "sfx_success";
    public const string Fail = "sfx_fail";
    public const string Mark = "sfx_mark";
    public const string Land = "sfx_land";
    public const string Cheer = "sfx_cheer";

    // 배경음
    public const string BgmRoom = "bgm_room";
    public const string BgmDraw = "bgm_draw";
    public const string BgmJudge = "bgm_judge";
    public const string BgmPicnic = "bgm_picnic";

    const string BgmKey = "volume_bgm";
    const string SfxKey = "volume_sfx";
    const float BgmBase = 0.5f;     // 배경음은 효과음보다 작게 깔아요 (볼륨 1 일 때 0.5)

    static float bgmVolume = -1f, sfxVolume = -1f;  // -1 = 아직 저장값을 안 읽음

    /// <summary>배경음 볼륨 0 ~ 1 (저장돼요)</summary>
    public static float BgmVolume
    {
        get { if (bgmVolume < 0f) bgmVolume = PlayerPrefs.GetFloat(BgmKey, 1f); return bgmVolume; }
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmKey, bgmVolume);
            PlayerPrefs.Save();
            if (bgmSource != null) bgmSource.volume = BgmBase * bgmVolume;
        }
    }

    /// <summary>효과음 볼륨 0 ~ 1 (저장돼요)</summary>
    public static float SfxVolume
    {
        get { if (sfxVolume < 0f) sfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f); return sfxVolume; }
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, sfxVolume);
            PlayerPrefs.Save();
        }
    }

    static AudioSource sfxSource;
    static AudioSource bgmSource;
    static string currentBgm;

    static void Prepare()
    {
        if (sfxSource != null) return;
        var go = new GameObject("Sound");
        Object.DontDestroyOnLoad(go);
        // 소리를 듣는 귀 : 씬 카메라에 AudioListener 가 없으면 아무 소리도 안 들려요. 없으면 여기에 하나 달아요
        if (Object.FindFirstObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
        sfxSource = go.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        bgmSource = go.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = BgmBase * BgmVolume;
    }

    public static void Play(string name)
    {
        var clip = Resources.Load<AudioClip>("Sound/" + name);
        if (clip == null) return;
        Prepare();
        if (SfxVolume <= 0f) return;
        sfxSource.PlayOneShot(clip, SfxVolume);
    }

    /// <summary>배경음 바꾸기. 이미 같은 곡이면 그대로 두고, null 이면 멈춰요.</summary>
    public static void Bgm(string name)
    {
        if (name == currentBgm) return;
        currentBgm = name;
        var clip = name != null ? Resources.Load<AudioClip>("Sound/" + name) : null;
        if (clip == null)
        {
            if (bgmSource != null) bgmSource.Stop();
            return;
        }
        Prepare();
        bgmSource.clip = clip;
        bgmSource.Play();
    }
}
