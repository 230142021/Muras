
using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    public Transform player;
    public DialogueManager dialogueManager;
    public AppleQuestManager appleQuestManager;

    public float interactionDistance = 3f;

    void Update()
    {
        if (player == null || dialogueManager == null)
            return;

        Vector3 npcPos = transform.position;
        Vector3 playerPos = player.position;

        npcPos.y = 0f;
        playerPos.y = 0f;

        float distance = Vector3.Distance(
            npcPos, playerPos
        );

        if (distance > interactionDistance)
        {
            if (dialogueManager.IsOpen)
                dialogueManager.CloseDialogue();

            return;
        }

        if (!dialogueManager.IsOpen)
            InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("поговорить с Айбеком", "talk to Aibek"));

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (dialogueManager.IsOpen)
            {
                dialogueManager.CloseDialogue();
            }
            else if (!dialogueManager.QuestCompleted)
            {
                dialogueManager.OpenDialogue();
            }
            else if (appleQuestManager != null)
            {
                appleQuestManager.InteractWithAibek();
            }
        }
    }
}
