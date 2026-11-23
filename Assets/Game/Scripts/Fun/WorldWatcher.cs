using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Следит за событиями мира: ночь, погода; открывает стартовые слова. Создаётся сам.</summary>
public class WorldWatcher : MonoBehaviour
{
    private float startAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name == PauseMenu.MenuScene || FindAnyObjectByType<WorldWatcher>() != null) return;
        new GameObject("WorldWatcher").AddComponent<WorldWatcher>();
    }

    void Start() => startAt = Time.time;

    void Update()
    {
        if (startAt >= 0f && Time.time - startAt > 4f)
        {
            startAt = -1f;
            Vocab.Unlock("kosh", "ail", "too");
        }
        if (DayNightCycle.Instance != null && DayNightCycle.IsNight && !Achievements.Has("night"))
        {
            if (!Vocab.Known("tun")) Vocab.Unlock("tun");
            Achievements.Give("night");
        }
    }
}
