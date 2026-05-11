using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    [Serializable]
    public class BgmEntry {
        public BgmId id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = true;
    }

    [Serializable]
    public class SfxEntry {
        public SfxId id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;

        [Header("Pitch Random")]
        [Range(0.1f, 3f)] public float pitchMin = 1f;
        [Range(0.1f, 3f)] public float pitchMax = 1f;
    }

    public BgmEntry[] bgms;
    public SfxEntry[] sfxs;
}
