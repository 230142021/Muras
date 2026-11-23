using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пока активна «Подсказка» (кнопка / H), ставит по золотой стрелке над целью каждого квеста:
/// Айбек и яблоки, юрта, Чүкө атуу, кухня.
/// </summary>
public class HintDirector : MonoBehaviour
{
    [Tooltip("Образец стрелки (с компонентом QuestArrow)")]
    public QuestArrow arrowTemplate;

    private readonly List<QuestArrow> pool = new List<QuestArrow>();

    private void Start()
    {
        if (arrowTemplate != null)
            arrowTemplate.Hide();
    }

    private void LateUpdate()
    {
        int used = 0;

        if (HintButton.IsActive && arrowTemplate != null)
        {
            foreach (IQuestHintSource source in QuestHints.Sources)
            {
                if (source is MonoBehaviour mb && (mb == null || !mb.isActiveAndEnabled))
                    continue;

                if (source.GetHintTarget(out Transform target, out float height) && target != null)
                    Get(used++).Show(target, height);
            }
        }

        for (int i = used; i < pool.Count; i++)
            pool[i].Hide();
    }

    private QuestArrow Get(int index)
    {
        while (pool.Count <= index)
        {
            QuestArrow arrow = Instantiate(arrowTemplate, transform);
            arrow.name = "QuestArrow_" + pool.Count;
            arrow.gameObject.SetActive(true);
            pool.Add(arrow);
        }
        return pool[index];
    }
}
