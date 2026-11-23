using UnityEngine;

/// <summary>Комузчу Нурлан: тихо играет свою күү (3D-звук), по E — ритм-игра на комузе.</summary>
public class KomuzMusician : MonoBehaviour
{
    public float talkDistance = 2.8f;
    private Transform player;
    private AudioSource ambient;

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        AudioClip c = MurasUI.Clip("komuz_jailoo");
        if (c != null)
        {
            ambient = gameObject.AddComponent<AudioSource>();
            ambient.clip = c; ambient.loop = true; ambient.spatialBlend = 1f; ambient.volume = 0.35f;
            ambient.minDistance = 2f; ambient.maxDistance = 16f; ambient.rolloffMode = AudioRolloffMode.Linear;
            ambient.Play();
        }
    }

    void Update()
    {
        if (ambient != null)
        {
            bool mute = KomuzGame.Instance != null && Modal.Current is KomuzGame;
            ambient.mute = mute;
        }
        if (player == null || !Modal.Free) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.sqrMagnitude > talkDistance * talkDistance) return;
        InteractionHint.Request("<b><color=#E8B04A>E</color></b>  —  " + Loc.T("сыграть на комузе с Нурланом", "play the komuz with Nurlan"));
        if (Input.GetKeyDown(KeyCode.E)) KomuzGame.Ensure().Open();
    }
}
