using UnityEngine;

/// <summary>Место работы Апы со шырдаком: подойди и нажми E, чтобы собрать узор.</summary>
public class ShyrdakStation : MonoBehaviour
{
    public float talkDistance = 2.4f;
    private Transform player;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
    }

    void Update()
    {
        if (player == null || !Modal.Free) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("помочь Апе сшить шырдак", "help Apa sew a shyrdak"));
        if (Input.GetKeyDown(KeyCode.E)) ShyrdakPuzzle.Ensure().Open();
    }
}
