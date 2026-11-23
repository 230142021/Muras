using UnityEngine;

/// <summary>Чабандес Эрмек у старта скачек: по E начинается ат чабыш.</summary>
public class RaceStarter : MonoBehaviour
{
    public RaceManager race;
    public float talkDistance = 3f;
    private Transform player;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        if (race == null) race = FindAnyObjectByType<RaceManager>();
    }

    void Update()
    {
        if (player == null || race == null || !Modal.Free) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;
        int wins = SaveData.Get("race_wins");
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("ат чабыш: скачки с Эрмеком", "at chabysh: race with Ermek") +
                                (wins > 0 ? "  <color=#C9BFA6>(" + Loc.T("побед: ", "wins: ") + wins + ")</color>" : ""));
        if (Input.GetKeyDown(KeyCode.E)) RaceMenu.Show(race);
    }
}
