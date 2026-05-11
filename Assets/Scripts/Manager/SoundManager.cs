using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Library")]
    [SerializeField] private SoundLibrary library;

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;

    [Header("AudioSources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Pool")]
    [SerializeField] private int sfxPoolSize = 6;
    [SerializeField] private Transform sfxPoolRoot;

    [Header("Rain Loop Source")]
    [SerializeField] private AudioSource rainLoopSource;
    [SerializeField] private AudioClip rainClip;
    [SerializeField, Range(0f, 1f)] private float rainBaseVolume = 1f;
    private bool isRainingLoopOn;

    private AudioSource[] sfxPool;
    private int sfxStealCursor;
    private readonly Dictionary<SfxId, int> lastClipIndexById = new();      //같은 sfxId에서 같은 클립 연속 방지용 딕셔너리
    private readonly Dictionary<SfxId, AudioSource> stoppableSfxSources = new();

    [Header("Exposed Parameter Names")]
    [SerializeField] private string masterParam = "MasterVol";
    [SerializeField] private string bgmParam = "BGMVol";
    [SerializeField] private string sfxParam = "SFXVol";

    [Header("Default Volume (0 ~ 1)")]
    [Range(0f, 1f)][SerializeField] private float defaultMaster = 0.8f;
    [Range(0f, 1f)][SerializeField] private float defaultBgm = 0.8f;
    [Range(0f, 1f)][SerializeField] private float defaultSfx = 0.8f;

    private const string KEY_MASTER = "vol_master";
    private const string KEY_BGM = "vol_bgm";
    private const string KEY_SFX = "vol_sfx";

    public event Action OnVolumeChanged;

    public float Master01 { get; private set; }
    public float Bgm01 { get; private set; }
    public float Sfx01 { get; private set; }

    private Dictionary<BgmId, SoundLibrary.BgmEntry> bgmMap;
    private Dictionary<SfxId, SoundLibrary.SfxEntry> sfxMap;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildMaps();

        BuildSfxPool();

        LoadVolumes();
        ApplyAllToMixer();
    }

    private void BuildMaps()
    {
        bgmMap = new Dictionary<BgmId, SoundLibrary.BgmEntry>();
        sfxMap = new Dictionary<SfxId, SoundLibrary.SfxEntry>();

        if (library == null) return;

        if(library.bgms != null)
        {
            foreach (var b in library.bgms)
            {
                if (b == null) continue;
                if (!bgmMap.ContainsKey(b.id)) bgmMap.Add(b.id, b);
                else Debug.LogWarning($"[SoundManager] Duplicate BGM id : {b.id}");
            }
        }

        if(library.sfxs != null)
        {
            foreach (var s in library.sfxs)
            {
                if (s == null) continue;
                if (!sfxMap.ContainsKey(s.id)) sfxMap.Add(s.id, s);
                else Debug.LogWarning($"[SoundManager] Duplicate SFX id : {s.id}");
            }
        }
    }

    private void BuildSfxPool()
    {
        if (sfxSource == null) return;

        if(sfxPoolRoot == null)
        {
            var root = new GameObject("SFX_Pool");
            root.transform.SetParent(transform);
            sfxPoolRoot = root.transform;
        }

        int size = Mathf.Max(1, sfxPoolSize);
        sfxPool = new AudioSource[size];

        for (int i = 0; i < size; i++)
        {
            var go = new GameObject($"SFX_{i:00}");
            go.transform.SetParent(sfxPoolRoot);

            var src = go.AddComponent<AudioSource>();
            CopySourceSettings(sfxSource, src);
            sfxPool[i] = src;
        }
    }

    private void CopySourceSettings(AudioSource from, AudioSource to)
    {
        to.outputAudioMixerGroup = from.outputAudioMixerGroup;

        to.playOnAwake = false;
        to.loop = false;

        to.spatialBlend = from.spatialBlend;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;

        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.priority = from.priority;
        to.panStereo = from.panStereo;
        to.reverbZoneMix = from.reverbZoneMix;

        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
    }

    private AudioSource GetSfxSource()
    {
        if(sfxPool == null || sfxPool.Length == 0)
            return sfxSource;
        
        for(int i = 0; i < sfxPool.Length; i++)
        {
            if (!sfxPool[i].isPlaying)
                return sfxPool[i];
        }

        //전부 재생 중이면 하나 훔쳐오기
        var src = sfxPool[sfxStealCursor++ % sfxPool.Length];
        src.Stop();
        return src;
    }

    private AudioSource GetOrCreateStoppableSource(SfxId id)
    {
        if (stoppableSfxSources.TryGetValue(id, out var src) && src != null)
            return src;

        var go = new GameObject($"SFX_Stoppable_{id}");
        go.transform.SetParent(transform, false);

        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;       //2D 사운드

        // 믹서 그룹 기본 설정을 원샷 소스와 동일하게 맞추기
        if(sfxSource != null)
        {
            src.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            src.priority = sfxSource.priority;
        }

        stoppableSfxSources[id] = src;
        return src;
    }

    private AudioClip PickRandom(AudioClip[] clips, SfxId id)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        int last = lastClipIndexById.TryGetValue(id, out var v) ? v : -1;

        //last 건너뛰게 보정
        int idx  = UnityEngine.Random.Range(0, clips.Length -1);
        if (last >= 0 && idx >= last) idx++;

        lastClipIndexById[id] = idx;
        return clips[idx];
    }


    private void LoadVolumes()
    {
        Master01 = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_MASTER, defaultMaster));
        Bgm01 = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_BGM, defaultBgm));
        Sfx01 = Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_SFX, defaultSfx));
    }

    private void SaveVolumes()
    {
        PlayerPrefs.SetFloat(KEY_MASTER, Master01);
        PlayerPrefs.SetFloat(KEY_BGM, Bgm01);
        PlayerPrefs.SetFloat(KEY_SFX, Sfx01);
        PlayerPrefs.Save();
    }

    private void ApplyAllToMixer()
    {
        ApplyToMixer(masterParam, Master01);
        ApplyToMixer(bgmParam, Bgm01);
        ApplyToMixer(sfxParam, Sfx01);

        OnVolumeChanged?.Invoke();
    }

    private void ApplyToMixer(string exposedParam, float volume01)
    {
        if (mixer == null) return;

        // 0 ~ 1 -> dB 변환 (0이면 -80db로 내려서 사실상 mute)
        float db = Volume01ToDb(volume01);
        mixer.SetFloat(exposedParam, db);
    }

    private float Volume01ToDb(float volume01)
    {
        volume01 = Mathf.Clamp01(volume01);
        if (volume01 <= 0.0001f) return -80f;       //Unity Mixer 최소치 느낌
        return Mathf.Log10(volume01) * 20f;         //표준 변환
    }

    #region Public API

    public void SetMaster01(float v01)
    {
        Master01 = Mathf.Clamp01(v01);
        ApplyToMixer(masterParam, Master01);
        SaveVolumes();
        OnVolumeChanged?.Invoke();
    }

    public void SetBgm01(float v01)
    {
        Bgm01 = Mathf.Clamp01(v01);
        ApplyToMixer(bgmParam, Bgm01);
        SaveVolumes(); 
        OnVolumeChanged?.Invoke();
    }

    public void SetSfx01(float v01)
    {
        Sfx01 = Mathf.Clamp01(v01);
        ApplyToMixer(sfxParam, Sfx01);
        SaveVolumes();
        OnVolumeChanged?.Invoke();
    }

    public void PlayBgm(BgmId id, bool restartIfSame = false, bool loopOverride = true)
    {
        if (bgmSource == null) return;

        if(id == BgmId.None)
        {
            StopBgm();
            return;
        }

        if(!bgmMap.TryGetValue(id, out var entry) || entry == null)
        {
            Debug.LogWarning($"[SoundManager] BGM entry not found : {id}");
            return;
        }

        var clip = PickRandom(entry.clips);
        if(clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM clip missing : {id}");
            return;
        }

        if (!restartIfSame && bgmSource.isPlaying && bgmSource.clip == clip)
            return;

        bgmSource.clip = clip;
        bgmSource.loop = loopOverride ? entry.loop : false;
        bgmSource.volume = Mathf.Clamp01(entry.volume);
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void PlaySfx(SfxId id, float volumeScale = 1f)
    {
        if (id == SfxId.None) return;

        if(!sfxMap.TryGetValue(id, out var entry)||  entry == null)
        {
            Debug.LogWarning($"[SoundManager] SFX entry not found : {id}");
            return;
        }

        var clip = PickRandom(entry.clips, id);
        if(clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX clip missing : {id}");
            return;
        }

        var src = GetSfxSource();
        if (src == null) return;

        float pMin = Mathf.Min(entry.pitchMin, entry.pitchMax);
        float pMax = Mathf.Max(entry.pitchMin, entry.pitchMax);
        src.pitch = (Mathf.Abs(pMax - pMin) < 0.0001f) ? pMin : UnityEngine.Random.Range(pMin, pMax);

        float finalVol = Mathf.Clamp01(volumeScale) * Mathf.Clamp01(entry.volume);
        src.PlayOneShot(clip, finalVol);
    }

    public void PlaySfxStoppable(SfxId id, float volumeScale = 1f, bool restartIfSame = true)
    {
        if (id == SfxId.None) return;

        if(!sfxMap.TryGetValue(id, out var entry) || entry == null)
        {
            Debug.LogWarning($"[SoundManager] SFX entry not found : {id}");
            return;
        }

        var clip = PickRandom(entry.clips);
        if(clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX clip missing : {id}");
            return;
        }

        var src = GetOrCreateStoppableSource(id);

        if (!restartIfSame && src.isPlaying && src.clip == clip)
            return;

        float pMin = Mathf.Min(entry.pitchMin, entry.pitchMax);
        float pMax = Mathf.Max(entry.pitchMax, entry.pitchMin);
        src.pitch = (Mathf.Abs(pMax - pMin) < 0.0001f) ? pMin : UnityEngine.Random.Range(pMin, pMax);

        src.volume = Mathf.Clamp01(volumeScale) * Mathf.Clamp01(entry.volume);
        src.clip = clip;
        src.loop = false;

        src.Play();
    }

    public void StopSfx(SfxId id)
    {
        if (id == SfxId.None) return;

        if (!stoppableSfxSources.TryGetValue(id, out var src) || src == null)
            return;

        src.Stop();
        src.clip = null;

    }

    #endregion

    private AudioClip PickRandom(AudioClip[] clips)
    {
        if(clips == null || clips.Length == 0) return null;
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    public void PlayRainLoop(float volumeScale = 1f)
    {
        if (rainLoopSource == null || rainClip == null) return;

        if (rainLoopSource.clip != rainClip)
            rainLoopSource.clip = rainClip;

        rainLoopSource.loop = true;
        rainLoopSource.volume = Mathf.Clamp01(rainBaseVolume);

        if(!rainLoopSource.isPlaying)
            rainLoopSource.Play();

        isRainingLoopOn = true;
    }

    public void StopRainLoop()
    {
        if (rainLoopSource == null) return;

        if(rainLoopSource.isPlaying)
        rainLoopSource.Stop();

        isRainingLoopOn = false;
    }

    public bool IsRainLoopOn => isRainingLoopOn;
}
