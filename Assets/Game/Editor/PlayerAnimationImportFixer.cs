using UnityEditor;
using UnityEngine;

/// <summary>
/// Включает зацикливание (Loop Time) у анимаций игрока и запекает вращение/высоту корня,
/// чтобы ходьба не останавливалась через один цикл и не уводила персонажа в сторону.
/// Срабатывает автоматически после компиляции; повторно: Tools > Diploma > Fix Player Animations.
/// </summary>
public static class PlayerAnimationImportFixer
{
    private const string Folder = "Assets/Game/Animations/Player/";

    private static readonly string[] LoopingClips =
    {
        Folder + "Idle.fbx",
        Folder + "Walking (1).fbx",
        Folder + "Running.fbx",
    };

    private const string JumpClip = Folder + "Jumping.fbx";

    // Кадры клипа прыжка Mixamo (30 fps): 13 — толчок, 34 — касание земли
    private const float JumpFirstFrame = 13f;
    private const float JumpLastFrame = 34f;

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        EditorApplication.delayCall += () => Fix(false);
    }

    [MenuItem("Tools/Diploma/Fix Player Animations")]
    private static void RunFromMenu()
    {
        Fix(true);
    }

    private static void Fix(bool alwaysLog)
    {
        int changed = 0;
        foreach (string path in LoopingClips)
            if (Apply(path, true)) changed++;
        if (Apply(JumpClip, false)) changed++;

        if (changed > 0 || alwaysLog)
            Debug.Log($"[PlayerAnimationImportFixer] Обновлено анимаций: {changed}");
    }

    private static bool Apply(string path, bool loop)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[PlayerAnimationImportFixer] Не найден файл: {path}");
            return false;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return false;

        bool dirty = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            dirty |= Set(clip.loopTime, loop, v => clip.loopTime = v);
            dirty |= Set(clip.loopPose, loop, v => clip.loopPose = v);

            // Вращение корня запекаем в позу: персонаж не разворачивается сам по себе.
            dirty |= Set(clip.lockRootRotation, true, v => clip.lockRootRotation = v);
            dirty |= Set(clip.keepOriginalOrientation, true, v => clip.keepOriginalOrientation = v);

            // Высоту тоже: естественное покачивание при шаге остаётся в анимации.
            dirty |= Set(clip.lockRootHeightY, true, v => clip.lockRootHeightY = v);

            if (path == JumpClip)
            {
                // Прыжок: высоту даёт физика, поэтому в анимации поза считается от ступней
                // (герой поджимает ноги, а не подпрыгивает второй раз), и вырезаем
                // долгое приседание перед толчком (кадры 0–12) и приземление (35–57).
                dirty |= Set(clip.keepOriginalPositionY, false, v => clip.keepOriginalPositionY = v);
                dirty |= Set(clip.heightFromFeet, true, v => clip.heightFromFeet = v);
                dirty |= SetF(clip.firstFrame, JumpFirstFrame, v => clip.firstFrame = v);
                dirty |= SetF(clip.lastFrame, JumpLastFrame, v => clip.lastFrame = v);
            }
            else
            {
                dirty |= Set(clip.keepOriginalPositionY, true, v => clip.keepOriginalPositionY = v);
            }

            // XZ не запекаем: персонажа двигает CharacterController, а не анимация.
            dirty |= Set(clip.lockRootPositionXZ, false, v => clip.lockRootPositionXZ = v);
        }

        if (!dirty)
            return false;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }

    private static bool SetF(float current, float wanted, System.Action<float> assign)
    {
        if (Mathf.Abs(current - wanted) < 0.01f)
            return false;
        assign(wanted);
        return true;
    }

    private static bool Set(bool current, bool wanted, System.Action<bool> assign)
    {
        if (current == wanted)
            return false;
        assign(wanted);
        return true;
    }
}
