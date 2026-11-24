using UnityEditor;
using UnityEngine;

/// <summary>Проверка нового окна диалога в Play Mode (две реплики разных NPC подряд — второй встаёт справа).</summary>
public static class DialogTestMenu
{
    static void Say(string line)
    {
        if (!Application.isPlaying || SpeechPanel.Instance == null || SpeechPanel.Instance.text == null) { Debug.Log("[Dialog] Запусти Play Mode"); return; }
        SpeechPanel.Instance.text.text = line;
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Dialog 1: Aibek")]
    static void A() => Say("Айбек: Салам, досум! Айылга кош келиң. Сначала зайди к аксакалу у юрты — он расскажет, как ставят боз үй.");

    [MenuItem("Tools/Diploma/Play Mode: Fun/Dialog 2: Aksakal answers")]
    static void B() => Say("Аксакал: Кел, балам! Боз үй — бул биздин мурас. Помоги мне собрать кереге.");

    [MenuItem("Tools/Diploma/Play Mode: Fun/Dialog 3: Narration")]
    static void C() => Say("Ты нашёл түндүк — верхний круг юрты. Отнеси его аксакалу.");

    [MenuItem("Tools/Diploma/Play Mode: Fun/Quest UI: Aibek question")]
    static void D()
    {
        if (!Application.isPlaying) return;
        foreach (var ni in Object.FindObjectsByType<NPCInteraction>(FindObjectsSortMode.None)) ni.interactionDistance = 9999f;
        var dm = Object.FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (dm != null) dm.OpenDialogue();
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Quest UI: Toggle yurt quiz")]
    static void E() => Toggle("QuizPanel");

    [MenuItem("Tools/Diploma/Play Mode: Fun/Quest UI: Toggle banner")]
    static void F()
    {
        if (!Application.isPlaying) return;
        var b = Object.FindAnyObjectByType<QuestCompleteBanner>(FindObjectsInactive.Include);
        if (b == null) return;
        b.showTime = 30f;
        typeof(QuestCompleteBanner).GetMethod("Show", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(b, new object[] { "yurt", 300 });
    }

    static void Toggle(string name)
    {
        if (!Application.isPlaying) return;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform t = c.transform.Find(name);
            if (t == null) continue;
            t.gameObject.SetActive(!t.gameObject.activeSelf);
            var cg = t.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 1f;
        }
    }

    [MenuItem("Tools/Diploma/Play Mode: Fun/Quest UI: Collapse or expand quests (K)")]
    static void G()
    {
        if (!Application.isPlaying) return;
        var t = Object.FindAnyObjectByType<QuestCardToggle>();
        if (t != null) t.Toggle();
    }
}
