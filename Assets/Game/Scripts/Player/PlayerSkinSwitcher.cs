using UnityEngine;

/// <summary>
/// Скины главного героя: каждая текстура-палитра красит части одежды в свои цвета.
/// Клавиша K переключает скин, выбор сохраняется между запусками.
/// </summary>
public class PlayerSkinSwitcher : MonoBehaviour
{
    public Renderer targetRenderer;
    public Texture2D[] skins;
    public string[] skinNames = { "Турист", "Кочевник", "Альпинист" };
    public string[] skinNamesEn = { "Tourist", "Nomad", "Climber" };
    public KeyCode switchKey = KeyCode.K;

    private const string SaveKey = "MURAS_PlayerSkin";
    private int current;
    private float hintUntil;
    private Material material;

    private void Start()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        if (targetRenderer != null)
            material = targetRenderer.material; // своя копия материала, ассет не меняется

        current = Mathf.Clamp(PlayerPrefs.GetInt(SaveKey, 0), 0, Mathf.Max(0, Count - 1));
        Apply();
    }

    private int Count => skins != null ? skins.Length : 0;

    private void Update()
    {
        if (Count > 1 && Input.GetKeyDown(switchKey))
        {
            current = (current + 1) % Count;
            PlayerPrefs.SetInt(SaveKey, current);
            PlayerPrefs.Save();
            Apply();
            hintUntil = Time.time + 1.8f;
        }

        if (Time.time < hintUntil)
            InteractionHint.Request(Loc.T("Скин: ", "Outfit: ") + "<b><color=#E8B04A>" + CurrentName + "</color></b>   (" +
                                    switchKey + Loc.T(" — следующий)", " — next)"));
    }

    private string CurrentName =>
        Loc.IsEnglish && skinNamesEn != null && current < skinNamesEn.Length ? skinNamesEn[current]
        : skinNames != null && current < skinNames.Length ? skinNames[current] : Loc.T("Скин ", "Outfit ") + (current + 1);

    private void Apply()
    {
        if (material == null || Count == 0 || skins[current] == null)
            return;

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", skins[current]);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", skins[current]);
    }
}
