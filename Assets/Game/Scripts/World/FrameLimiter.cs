using UnityEngine;

/// <summary>
/// Ограничивает частоту кадров (по настройке графики). Без лимита Unity рисует сотни кадров в секунду,
/// и ноутбук греется впустую. Запускается сам при старте игры.
/// </summary>
public static class FrameLimiter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        GameSettings.ApplyGraphics();   // 30 к/с на «Низком», 60 — на «Среднем» и «Высоком»
    }
}
