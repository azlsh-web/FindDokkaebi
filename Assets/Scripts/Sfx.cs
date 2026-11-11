using UnityEngine;

// ─────────────────────────────────────────────────────────────
// Sfx : 효과음 · 배경음 재생
// ─────────────────────────────────────────────────────────────
// Assets/Resources/Sound 폴더에 아래 이름의 파일을 넣으면 바로 소리가 나요.
// 파일이 없으면 조용히 넘어가서, 소리가 아직 없어도 게임은 그대로 돌아가요.
// 필요한 소리 목록 : Docs/전달/12_사운드팀_전달사항.pdf
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

    static AudioSource sfxSource;
    static AudioSource bgmSource;
    static string currentBgm;

    static void Prepare()
    {
        if (sfxSource != null) return;
        var go = new GameObject("Sound");
        Object.DontDestroyOnLoad(go);
        sfxSource = go.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        bgmSource = go.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = 0.5f;
    }

    public static void Play(string name)
    {
        var clip = Resources.Load<AudioClip>("Sound/" + name);
        if (clip == null) return;
        Prepare();
        sfxSource.PlayOneShot(clip);
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
