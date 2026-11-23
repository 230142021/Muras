
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FoodQuestManager : MonoBehaviour, IQuestHintSource
{
    [Header("Characters")]
    public Transform player;
    public Transform ezhe;
    public Transform cookingStation;

    [Header("Boorsok Ingredients")]
    public GameObject[] boorsokIngredients = new GameObject[3];

    [Header("Kuurdak Ingredients")]
    public GameObject[] kuurdakIngredients = new GameObject[3];

    [Header("Finished Dishes")]
    public GameObject boorsokDish;
    public GameObject kuurdakDish;

    [Header("Interface")]
    public TMP_Text questText;
    public TMP_Text scoreText;
    public TMP_Text storyText;
    public TMP_Text interactionText;

    [Header("Story Panel")]
    public GameObject storyPanel;
    public float storyDuration = 5f;

    [Header("Quiz")]
    public GameObject quizPanel;
    public TMP_Text questionText;
    public TMP_Text feedbackText;
    public Button[] answerButtons = new Button[3];

    [Header("Settings")]
    public float interactionDistance = 3f;
    [Tooltip("С какого расстояния (м) можно взять продукт — подойди к столу вплотную")]
    public float pickupRadius = 1.3f;
    public int rewardPerDish = 200;
    [Tooltip("Сколько секунд готовится блюдо в казане")]
    public float cookingTime = 3f;

    private enum QuestState
    {
        TalkToEzhe,
        CollectIngredients,
        Cook,
        Cooking,
        Quiz,
        NextRecipe,
        Completed
    }

    private QuestState state = QuestState.TalkToEzhe;

    private int recipeIndex = 0;
    private int collectedCount = 0;

    private bool[] collected = new bool[3];

    private Coroutine storyCoroutine;
    private int currentCorrect = -1;
    private Coroutine quizCoroutine;

    private void OnEnable() { QuestHints.Register(this); Loc.Changed += UpdateUI; }
    private void OnDisable() { QuestHints.Unregister(this); Loc.Changed -= UpdateUI; }

    public bool GetHintTarget(out Transform target, out float height)
    {
        target = null;
        height = 2.6f;
        switch (state)
        {
            case QuestState.TalkToEzhe:
            case QuestState.NextRecipe:
                target = ezhe;
                break;
            case QuestState.Cook:
                target = cookingStation;
                height = 2.2f;
                break;
            case QuestState.CollectIngredients:
                GameObject[] items = CurrentIngredients();
                float best = float.MaxValue;
                for (int i = 0; items != null && i < items.Length && i < 3; i++)
                {
                    if (collected[i] || items[i] == null || !items[i].activeInHierarchy) continue;
                    float d = player != null ? (items[i].transform.position - player.position).sqrMagnitude : i;
                    if (d < best) { best = d; target = items[i].transform; }
                }
                height = 1.6f;
                break;
        }
        return target != null;
    }

    private void Start()
    {
        if (storyPanel != null)
            storyPanel.SetActive(false);

        if (storyText != null)
            storyText.text = "";

        if (interactionText != null)
            interactionText.text = "";

        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (feedbackText != null)
            feedbackText.text = "";

        // Ингредиенты куурдака появляются, когда эже даст второй рецепт
        if (recipeIndex == 0)
            SetIngredientsActive(kuurdakIngredients, false);

        LoadQuestProgress();
        UpdateUI();
    }

    private void SetIngredientsActive(GameObject[] items, bool active)
    {
        if (items == null) return;
        foreach (GameObject item in items)
            if (item != null) item.SetActive(active);
    }

    private void Update()
    {
        if (player == null)
            return;

        UpdateInteraction();

        // Ответ в опросе клавишами 1, 2, 3
        if (state == QuestState.Quiz && currentCorrect >= 0)
            for (int i = 0; i < answerButtons.Length && i < 9; i++)
                if (answerButtons[i] != null && answerButtons[i].gameObject.activeInHierarchy &&
                    answerButtons[i].interactable &&
                    (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)))
                {
                    CheckAnswer(i, currentCorrect);
                    break;
                }

        if (Input.GetKeyDown(KeyCode.E))
            HandleInteraction();
    }

    private GameObject[] CurrentIngredients()
    {
        return recipeIndex == 0
            ? boorsokIngredients
            : kuurdakIngredients;
    }

    // «Суу (вода)» → «Суу (water)» в английской версии
    private static string ItemLabel(string n)
    {
        if (!Loc.IsEnglish) return n;
        return n.Replace("(вода)", "(water)").Replace("(масло)", "(oil)").Replace("(мука)", "(flour)")
                .Replace("(мясо)", "(meat)").Replace("(лук)", "(onion)").Replace("Картошка", "Картошка (potato)");
    }

    private string CurrentRecipeName()
    {
        return recipeIndex == 0
            ? "Боорсок"
            : "Куурдак";
    }

    private string CurrentQuestId()
    {
        return recipeIndex == 0
            ? "boorsok"
            : "kuurdak";
    }

    private bool IsNear(Transform target)
    {
        if (target == null || player == null)
            return false;

        return Vector3.Distance(
            player.position,
            target.position
        ) <= interactionDistance;
    }

    private int FindNearbyIngredient()
    {
        GameObject[] ingredients = CurrentIngredients();

        if (ingredients == null)
            return -1;

        int nearestIndex = -1;
        float nearestDistance = pickupRadius;

        for (int i = 0; i < ingredients.Length && i < 3; i++)
        {
            if (collected[i])
                continue;

            GameObject item = ingredients[i];

            if (item == null || !item.activeInHierarchy)
                continue;

            // по горизонтали: продукты лежат на столах и на земле
            Vector3 a = player.position, b = item.transform.position;
            a.y = 0f; b.y = 0f;
            float distance = Vector3.Distance(a, b);

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private void HandleInteraction()
    {
        switch (state)
        {
            case QuestState.TalkToEzhe:
                if (IsNear(ezhe))
                    TalkToEzhe();
                break;

            case QuestState.CollectIngredients:
                int index = FindNearbyIngredient();

                if (index >= 0)
                    CollectIngredient(index);
                break;

            case QuestState.Cook:
                if (IsNear(cookingStation))
                    CookRecipe();
                break;

            case QuestState.NextRecipe:
                if (IsNear(ezhe))
                    StartNextRecipe();
                break;
        }
    }

    private void TalkToEzhe()
    {
        state = QuestState.CollectIngredients;

        if (recipeIndex == 0)
        {
            ShowStory(
                "Эже: Кош келдиң! " + Loc.T("Сегодня приготовим боорсок. Собери муку, воду и масло у левого стола.",
                    "Today we'll cook боорсок. Collect flour, water and oil at the left table.")
            );
        }
        else
        {
            ShowStory(
                "Эже: " + Loc.T("Теперь приготовим куурдак. Собери мясо, лук и картошку на правом столе.",
                    "Now let's cook куурдак. Collect meat, onion and potatoes at the right table.")
            );
        }

        UpdateUI();
    }

    private void CollectIngredient(int index)
    {
        GameObject[] ingredients = CurrentIngredients();

        if (index < 0 ||
            index >= ingredients.Length ||
            ingredients[index] == null)
            return;

        if (collected[index])
            return;

        string itemName = ingredients[index].name;

        collected[index] = true;
        GameAudio.Pickup();
        collectedCount++;

        ingredients[index].SetActive(false);

        ShowStory(Loc.T("Получено: ", "Got: ") + ItemLabel(itemName));

        if (collectedCount >= 3)
        {
            state = QuestState.Cook;

            ShowStory(
                Loc.T("Все ингредиенты собраны! Подойди к казану под навесом и нажми E.",
                      "All ingredients collected! Go to the kazan under the canopy and press E.")
            );
        }

        UpdateUI();
    }

    private void CookRecipe()
    {
        if (state != QuestState.Cook)
            return;
        StartCoroutine(CookRoutine());
    }

    // Блюдо готовится несколько секунд, эже комментирует процесс
    private IEnumerator CookRoutine()
    {
        state = QuestState.Cooking;
        UpdateUI();

        string[] steps = recipeIndex == 0
            ? (Loc.IsEnglish
                ? new[] { "Эже: Kneading dough from flour and water...", "Эже: Cutting the dough into diamonds...", "Эже: Frying боорсок in hot oil — shhh!" }
                : new[] { "Эже: Замешиваем тесто из муки и воды...", "Эже: Режем тесто ромбиками...", "Эже: Жарим боорсок в горячем масле — шшш!" })
            : (Loc.IsEnglish
                ? new[] { "Эже: Browning the meat in the kazan...", "Эже: Adding the onion...", "Эже: Adding potatoes and stewing — куурдак is almost ready!" }
                : new[] { "Эже: Обжариваем мясо в казане...", "Эже: Добавляем лук...", "Эже: Кладём картошку и тушим — куурдак почти готов!" });

        float step = Mathf.Max(0.3f, cookingTime / steps.Length);
        foreach (string line in steps)
        {
            ShowStory(line);
            yield return new WaitForSeconds(step);
        }

        FinishCooking();
    }

    private void FinishCooking()
    {
        if (recipeIndex == 0)
        {
            if (boorsokDish != null)
                boorsokDish.SetActive(true);
        }
        else
        {
            if (kuurdakDish != null)
                kuurdakDish.SetActive(true);
        }

        state = QuestState.Quiz;

        ShowStory(
            CurrentRecipeName() +
            Loc.T(" готов — он уже на дасторконе! Ответь на вопрос эже.", " is ready — it's on the дасторкон! Answer эже's question.")
        );

        StartQuiz();
        UpdateUI();
    }

    private void StartQuiz()
    {
        if (storyCoroutine != null)
        {
            StopCoroutine(storyCoroutine);
            storyCoroutine = null;
        }

        HideStory();

        if (quizPanel != null)
            quizPanel.SetActive(true);

        if (feedbackText != null)
            feedbackText.text = "";

        if (recipeIndex == 0)
        {
            SetupQuiz(
                Loc.T("Как традиционно готовят боорсок?", "How is боорсок traditionally cooked?"),
                new string[]
                {
                    Loc.T("Жарят в масле", "Fried in oil"),
                    Loc.T("Варят в воде", "Boiled in water"),
                    Loc.T("Запекают без масла", "Baked without oil")
                },
                0
            );
        }
        else
        {
            SetupQuiz(
                Loc.T("Какой основной ингредиент куурдака?", "What is the main ingredient of куурдак?"),
                new string[]
                {
                    Loc.T("Мука", "Flour"),
                    Loc.T("Мясо", "Meat"),
                    Loc.T("Рис", "Rice")
                },
                1
            );
        }
    }

    private void SetupQuiz(
        string question,
        string[] answers,
        int correctAnswer)
    {
        currentCorrect = correctAnswer;

        if (questionText != null)
            questionText.text = question;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            Button button = answerButtons[i];

            if (button == null)
                continue;

            button.onClick.RemoveAllListeners();
            button.interactable = true;

            bool valid = i < answers.Length;
            button.gameObject.SetActive(valid);

            if (!valid)
                continue;

            TMP_Text buttonText =
                button.GetComponentInChildren<TMP_Text>();

            if (buttonText != null)
                buttonText.text = answers[i];

            int selectedIndex = i;

            button.onClick.AddListener(() =>
                CheckAnswer(selectedIndex, correctAnswer));
        }
    }

    private void CheckAnswer(int selectedIndex, int correctIndex)
    {
        if (state != QuestState.Quiz)
            return;

        if (selectedIndex != correctIndex)
        {
            if (feedbackText != null)
                feedbackText.text = "<color=#ED665C>" + Loc.T("Туура эмес! Попробуй ещё раз.", "Туура эмес! Try again.") + "</color>";

            GameAudio.Wrong();
            return;
        }

        if (feedbackText != null)
            feedbackText.text = "<color=#5FC873>" + Loc.T("Туура! Правильно!", "Туура! Correct!") + "</color>";

        GameAudio.Correct();

        foreach (Button button in answerButtons)
        {
            if (button != null)
                button.interactable = false;
        }

        if (quizCoroutine != null)
            StopCoroutine(quizCoroutine);

        quizCoroutine = StartCoroutine(FinishQuizAfterDelay());
    }

    private IEnumerator FinishQuizAfterDelay()
    {
        yield return new WaitForSeconds(1.5f);

        quizCoroutine = null;
        FinishRecipe();
    }

    private void FinishRecipe()
    {
        if (quizPanel != null)
            quizPanel.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CompleteQuest(
                CurrentQuestId(),
                rewardPerDish
            );
        }

        if (recipeIndex == 0)
        {
            state = QuestState.NextRecipe;

            ShowStory(
                Loc.T("Отлично! Боорсок готов. Вернись к эже, чтобы научиться готовить куурдак.",
                      "Great! Боорсок is ready. Go back to эже to learn how to cook куурдак.")
            );
        }
        else
        {
            state = QuestState.Completed;

            ShowStory(
                Loc.T("Поздравляем! Ты приготовил боорсок и куурдак. Задания национальной кухни выполнены!",
                      "Congratulations! You cooked боорсок and куурдак. The national cuisine quests are complete!")
            );
        }

        UpdateUI();
    }

    private void StartNextRecipe()
    {
        recipeIndex = 1;

        collected = new bool[3];
        collectedCount = 0;
        SetIngredientsActive(kuurdakIngredients, true);

        state = QuestState.CollectIngredients;

        ShowStory(
            "Эже: " + Loc.T("Теперь приготовим куурдак! Собери мясо, лук и картошку на правом столе и неси к казану.",
                "Now let's cook куурдак! Collect meat, onion and potatoes at the right table and bring them to the kazan.")
        );

        UpdateUI();
    }

    private void LoadQuestProgress()
    {
        if (boorsokDish != null)
            boorsokDish.SetActive(false);

        if (kuurdakDish != null)
            kuurdakDish.SetActive(false);

        if (GameManager.Instance == null)
            return;

        bool boorsokDone =
            GameManager.Instance.IsQuestCompleted("boorsok");

        bool kuurdakDone =
            GameManager.Instance.IsQuestCompleted("kuurdak");

        if (boorsokDone)
        {
            if (boorsokDish != null)
                boorsokDish.SetActive(true);

            HideIngredients(boorsokIngredients);

            recipeIndex = 1;
            state = QuestState.NextRecipe;
        }

        if (kuurdakDone)
        {
            if (kuurdakDish != null)
                kuurdakDish.SetActive(true);

            HideIngredients(kuurdakIngredients);

            recipeIndex = 1;
            state = QuestState.Completed;
        }
    }

    private void HideIngredients(GameObject[] ingredients)
    {
        if (ingredients == null)
            return;

        foreach (GameObject item in ingredients)
        {
            if (item != null)
                item.SetActive(false);
        }
    }

    private void UpdateInteraction()
    {
        string message = "";

        switch (state)
        {
            case QuestState.TalkToEzhe:
                if (IsNear(ezhe))
                    message = Key(Loc.T("поговорить с эже", "talk to эже"));
                break;

            case QuestState.CollectIngredients:
                int index = FindNearbyIngredient();

                if (index >= 0)
                    message = Key(Loc.T("подобрать: ", "pick up: ") + ItemLabel(CurrentIngredients()[index].name));
                break;

            case QuestState.Cook:
                if (IsNear(cookingStation))
                    message = Key(Loc.T("приготовить ", "cook ") + CurrentRecipeName());
                break;

            case QuestState.NextRecipe:
                if (IsNear(ezhe))
                    message = Key(Loc.T("начать готовить куурдак", "start cooking куурдак"));
                break;
        }

        if (interactionText != null)
            interactionText.text = message;
        else if (!string.IsNullOrEmpty(message))
            InteractionHint.Request(message);
    }

    private static string Key(string action) => "<b><color=#E8B04A>E</color></b>  —  " + action;

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text =
                Loc.T("Ингредиенты: ", "Ingredients: ") + collectedCount + "/3";

        if (questText == null)
            return;

        string title = "<b>Улуттук тамак</b>";
        string count = "  <color=#E8B04A>" + collectedCount + "/3</color>";
        string Sub(string text) => "\n<size=85%><color=#C9BFA6>" + text + "</color></size>";

        switch (state)
        {
            case QuestState.TalkToEzhe:
                questText.text = title + Sub(Loc.T("Поговори с эже на кухне", "Talk to эже in the kitchen"));
                break;
            case QuestState.CollectIngredients:
                questText.text = title + count + Sub(Loc.T("Собери продукты для: ", "Collect ingredients for: ") + CurrentRecipeName());
                break;
            case QuestState.Cook:
                questText.text = title + "  <color=#5FC873>3/3</color>" + Sub(Loc.T("Приготовь ", "Cook ") + CurrentRecipeName() + Loc.T(" у казана", " at the kazan"));
                break;
            case QuestState.Cooking:
                questText.text = title + "  <color=#5FC873>3/3</color>" + Sub(Loc.T("Готовим ", "Cooking ") + CurrentRecipeName() + "...");
                break;
            case QuestState.Quiz:
                questText.text = title + Sub(Loc.T("Ответь на вопрос эже", "Answer эже's question"));
                break;
            case QuestState.NextRecipe:
                questText.text = title + "  <color=#5FC873>1/2</color>" + Sub(Loc.T("Вернись к эже за новым рецептом", "Return to эже for a new recipe"));
                break;
            case QuestState.Completed:
                questText.text = title + "  <color=#5FC873>2/2</color>" + Sub(Loc.T("Боорсок и куурдак готовы", "Боорсок and куурдак are ready"));
                break;
        }
    }

    private void ShowStory(string message)
    {
        if (storyCoroutine != null)
            StopCoroutine(storyCoroutine);

        if (string.IsNullOrWhiteSpace(message))
        {
            HideStory();
            return;
        }

        if (storyText != null)
            storyText.text = message;

        if (storyPanel != null)
            storyPanel.SetActive(true);

        storyCoroutine = StartCoroutine(HideStoryAfterDelay());
    }

    private IEnumerator HideStoryAfterDelay()
    {
        yield return new WaitForSeconds(storyDuration);

        HideStory();
        storyCoroutine = null;
    }

    private void HideStory()
    {
        if (storyText != null)
            storyText.text = "";

        if (storyPanel != null)
            storyPanel.SetActive(false);
    }
}
