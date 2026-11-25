using UnityEngine;

// ─────────────────────────────────────────────────────────────
// Sfx : 효과음 · 배경음 재생
// ─────────────────────────────────────────────────────────────
// Assets/Resources/Sound 폴더에 아래 이름의 파일을 넣으면 바로 소리가 나요.
// 파일이 없으면 조용히 넘어가서, 소리가 아직 없어도 게임은 그대로 돌아가요.
// 필요한 소리 목록 : Docs/3_전달/사운드.pdf
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
    // 5챕터 (임시 : Claude 가 합성 · 변형, Tools/sound/make_temp_sound.py, C-65)
    public const string Scare = "sfx_scare";            // 놀래키기 · 점프스케어
    public const string HeartLoss = "sfx_heart_loss";   // 주인공 하트 -1
    public const string Interfere = "sfx_interfere";    // 그리기 방해 빨간 낙서
    public const string EndingHappy = "sfx_ending_happy";
    public const string GameOver = "sfx_gameover";

    // 배경음
    public const string BgmRoom = "bgm_room";
    public const string BgmDraw = "bgm_draw";
    public const string BgmJudge = "bgm_judge";
    public const string BgmPicnic = "bgm_picnic";
    public const string BgmCurse = "bgm_curse";     // 5챕터 흑화 뒤 (임시 : bgm_judge 를 느리고 낮게 바꾼 곡, D1)
    public const string BgmEvening = "bgm_evening"; // 3챕터 저녁 방 (임시 : bgm_room 을 조금 느리고 어둡게, C-65)
    public const string BgmNight = "bgm_night";     // 4챕터 밤 방 (임시 : bgm_room 을 더 느리고 낮게, C-65)

    // 환경음 : 배경음 위에 겹쳐 깔아요
    public const string AmbRain = "amb_rain";       // 2챕터 비 오는 방 (임시 : 합성 빗소리, C-65)

    const string BgmKey = "volume_bgm";
    const string SfxKey = "volume_sfx";
    const float BgmBase = 0.5f;     // 배경음은 효과음보다 작게 깔아요 (볼륨 1 일 때 0.5)
    const float AmbBase = 0.35f;    // 환경음은 배경음보다 더 작게, 대사를 가리지 않게 (배경음 볼륨을 따라가요)

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
            if (ambSource != null) ambSource.volume = AmbBase * bgmVolume;
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
    static AudioSource ambSource;
    static string currentBgm, currentAmb;

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
        ambSource = go.AddComponent<AudioSource>();
        ambSource.playOnAwake = false;
        ambSource.loop = true;
        ambSource.volume = AmbBase * BgmVolume;
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

    /// <summary>지금 배경음 이름 (없으면 null)</summary>
    public static string CurrentBgm => currentBgm;

    /// <summary>지금 깔린 환경음 이름 (없으면 null)</summary>
    public static string CurrentAmbient => currentAmb;

    /// <summary>환경음 바꾸기 (배경음과 따로 겹쳐 나요). 이미 같으면 그대로, null 이면 멈춰요.</summary>
    public static void Ambient(string name)
    {
        if (name == currentAmb) return;
        currentAmb = name;
        var clip = name != null ? Resources.Load<AudioClip>("Sound/" + name) : null;
        if (clip == null)
        {
            if (ambSource != null) ambSource.Stop();
            return;
        }
        Prepare();
        ambSource.clip = clip;
        ambSource.Play();
    }
}
