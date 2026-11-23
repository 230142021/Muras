using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject dialoguePanel;
    public TMP_Text npcNameText;
    public TMP_Text questionText;
    public TMP_Text translationText;
    public TMP_Text feedbackText;

    public Button[] answerButtons;
    public Button closeButton;

    [Header("Quest Settings")]
    public string npcName = "Айбек";
    public string question = "Салам! Атың ким?";
    public string translation = "Привет! Как тебя зовут?";
    public string translationEn = "Hello! What is your name?";

    public string[] answers =
    {
        "Менин атым Азамат.",
        "Мен жакшымын.",
        "Мен Бишкекте жашайм."
    };

    public int correctAnswerIndex = 0;
    public int rewardXP = 50;

    [Header("Colors")]
    public Color correctColor = new Color(0.36f, 0.78f, 0.45f);
    public Color wrongColor = new Color(0.93f, 0.40f, 0.36f);
    public Color neutralColor = new Color(0.85f, 0.80f, 0.68f);

    private const string QuestId = "greeting";

    private bool questCompleted = false;
    public bool QuestCompleted
    {
        get { return questCompleted; }
    }
    private bool answered = false;

    public bool IsOpen
    {
        get { return dialoguePanel != null && dialoguePanel.activeSelf; }
    }

    void Start()
    {
        dialoguePanel.SetActive(false);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i;

            answerButtons[i].onClick.AddListener(
                () => CheckAnswer(index)
            );
        }

        closeButton.onClick.AddListener(CloseDialogue);

        if (GameManager.Instance != null &&
            GameManager.Instance.IsQuestCompleted(QuestId))
        {
            questCompleted = true;
            answered = true;
        }
    }

    void Update()
    {
        if (!IsOpen || answered || questCompleted)
            return;

        // Ответ клавишами 1, 2, 3.
        for (int i = 0; i < answerButtons.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                CheckAnswer(i);
                break;
            }
        }
    }

    public void OpenDialogue()
    {
        if (dialoguePanel == null) return;

        dialoguePanel.SetActive(true);
        answered = false;

        npcNameText.text = npcName;
        questionText.text = question;
        GameAudio.Babble(question, 1.0f);
        translationText.text = Loc.T(translation, translationEn);

        if (questCompleted)
            SetFeedback(Loc.T("Задание уже выполнено!", "Quest already completed!"), neutralColor);
        else
            SetFeedback(Loc.T("Выбери ответ — мышью или клавишами 1, 2, 3", "Choose an answer — mouse or keys 1, 2, 3"), neutralColor);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool hasAnswer = i < answers.Length;

            SetAnswerVisible(i, hasAnswer && !questCompleted);
            SetAnswerTint(i, Color.white);

            if (hasAnswer)
            {
                TMP_Text buttonText =
                    answerButtons[i].GetComponentInChildren<TMP_Text>();

                buttonText.text = answers[i];
                answerButtons[i].interactable = true;
            }
        }
    }

    public void CheckAnswer(int selectedIndex)
    {
        Debug.Log("Clicked answer: " + selectedIndex);

        if (answered || questCompleted) return;
        if (selectedIndex < 0 || selectedIndex >= answerButtons.Length) return;
        if (!answerButtons[selectedIndex].gameObject.activeInHierarchy) return;

        if (selectedIndex == correctAnswerIndex)
        {
            answered = true;
            questCompleted = true;

            SetFeedback(
                Loc.T("Туура! Правильно! +", "Туура! Correct! +") + rewardXP + " XP\n" +
                Loc.T("<size=80%>Нажми E, чтобы закрыть — у Айбека есть для тебя задание</size>",
                      "<size=80%>Press E to close — Aibek has a task for you</size>"),
                correctColor);
            SetAnswerTint(selectedIndex, correctColor);
            GameAudio.Correct();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.CompleteQuest(QuestId, rewardXP);
            }

            foreach (Button button in answerButtons)
            {
                button.interactable = false;
            }

            Debug.Log("Quest completed!");
        }
        else
        {
            SetFeedback(Loc.T("Туура эмес! Неправильно, попробуй ещё раз.", "Туура эмес! Wrong, try again."), wrongColor);
            SetAnswerTint(selectedIndex, wrongColor);
            GameAudio.Wrong();
        }
    }

    public void CloseDialogue()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    void SetFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
    }

    // Если кнопка лежит внутри красивой подложки (AnswerSlot), прячем/показываем всю подложку.
    GameObject AnswerRoot(int index)
    {
        Transform t = answerButtons[index].transform;
        if (t.parent != null && t.parent.name.StartsWith("AnswerSlot"))
            return t.parent.gameObject;
        return t.gameObject;
    }

    void SetAnswerVisible(int index, bool visible)
    {
        answerButtons[index].gameObject.SetActive(visible);
        GameObject root = AnswerRoot(index);
        if (root != answerButtons[index].gameObject)
            root.SetActive(visible);
    }

    void SetAnswerTint(int index, Color color)
    {
        GameObject root = AnswerRoot(index);
        if (root == answerButtons[index].gameObject) return;

        Transform accent = root.transform.Find("Accent");
        if (accent != null && accent.TryGetComponent(out Image img))
            img.color = color == Color.white ? new Color(0.84f, 0.66f, 0.29f) : color;
    }
}
