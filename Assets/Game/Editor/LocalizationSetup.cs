using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Подключает английский язык к постоянным надписям села (заголовки окон, подписи кнопок)
/// и к ролям NPC в панели реплик. Тексты, которые пишут квесты, переводятся в самих скриптах.
/// Запуск: Tools > Diploma > Setup Localization (Village). Можно запускать повторно.
/// </summary>
public static class LocalizationSetup
{
    private const string ScenePath = "Assets/Game/Scenes/Village.unity";

    private static readonly Dictionary<string, string> StaticTexts = new Dictionary<string, string>
    {
        { "ЗАДАНИЕ", "QUEST" },
        { "Вопрос", "Question" },
        { "СУРОО  ·  ОПРОС", "СУРОО  ·  QUIZ" },
        { "ТАПШЫРМА АТКАРЫЛДЫ  ·  КВЕСТ ВЫПОЛНЕН", "ТАПШЫРМА АТКАРЫЛДЫ  ·  QUEST COMPLETE" },
        { "<b>КҮЧ</b>  ·  сила броска", "<b>КҮЧ</b>  ·  throw power" },
        { "<color=#E8B04A><b>E</b></color> — закрыть", "<color=#E8B04A><b>E</b></color> — close" },
        { "мышь или клавиши <color=#E8B04A><b>1  2  3</b></color>", "mouse or keys <color=#E8B04A><b>1  2  3</b></color>" },
    };

    private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
    {
        { "старейшина села", "village elder" },
        { "старейшина", "elder" },
        { "житель села", "villager" },
        { "мальчик с чүкө", "boy who plays чүкө" },
        { "девочка", "girl" },
        { "мастерица кухни", "master cook" },
    };

    [MenuItem("Tools/Diploma/Setup Localization (Village)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int texts = 0, roles = 0;

        foreach (TMP_Text t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
        {
            string current = t.TryGetComponent(out LocalizedText existing) ? existing.ru : t.text;
            if (current == null || !StaticTexts.TryGetValue(current, out string en)) continue;
            if (existing == null) existing = t.gameObject.AddComponent<LocalizedText>();
            existing.ru = current;
            existing.en = en;
            EditorUtility.SetDirty(existing);
            texts++;
        }

        foreach (SpeechPanel panel in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpeechPanel>(true)))
        {
            foreach (SpeechPanel.Speaker s in panel.speakers)
                if (s.role != null && Roles.TryGetValue(s.role.Trim(), out string en)) { s.roleEn = en; roles++; }
            EditorUtility.SetDirty(panel);
        }

        foreach (DialogueManager dm in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueManager>(true)))
        {
            if (dm.translation == "Привет! Как тебя зовут?") dm.translationEn = "Hello! What is your name?";
            EditorUtility.SetDirty(dm);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Localization] Village: надписей переведено {texts}, ролей NPC {roles}. Язык меняется в Настройках (меню или пауза).");
    }
}
