using System.Collections;
using UnityEngine;

/// <summary>
/// Фоновая музыка главного меню: зацикленная, плавно появляется при старте
/// и плавно затихает при переходе в игру (MenuMusic.FadeOutAll).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MenuMusic : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.55f;
    public float fadeInTime = 3f;

    private static MenuMusic current;
    private AudioSource source;

    void Awake()
    {
        current = this;
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;
    }

    void Start()
    {
        if (source.clip == null) return;
        source.Play();
        StartCoroutine(FadeTo(volume, fadeInTime));
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    /// <summary>Плавно заглушить музыку меню (например, перед загрузкой села).</summary>
    public static void FadeOutAll(float time)
    {
        if (current != null && current.isActiveAndEnabled)
        {
            current.StopAllCoroutines();
            current.StartCoroutine(current.FadeTo(0f, time));
        }
    }

    IEnumerator FadeTo(float target, float time)
    {
        float start = source.volume;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        source.volume = target;
    }
}
