using System.Collections.Generic;
using UnityEngine;

/// <summary>Любой квест, который умеет показывать подсказку-стрелку.</summary>
public interface IQuestHintSource
{
    /// <summary>Куда сейчас идти игроку. false — подсказки нет (квест не активен или выполнен).</summary>
    bool GetHintTarget(out Transform target, out float height);
}

/// <summary>Реестр квестов с подсказками.</summary>
public static class QuestHints
{
    private static readonly List<IQuestHintSource> sources = new List<IQuestHintSource>();
    public static IReadOnlyList<IQuestHintSource> Sources => sources;

    public static void Register(IQuestHintSource source)
    {
        if (source != null && !sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(IQuestHintSource source) => sources.Remove(source);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => sources.Clear();
}
