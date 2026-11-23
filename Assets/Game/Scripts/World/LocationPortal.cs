
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocationPortal : MonoBehaviour
{
    public Transform player;
    public string targetScene = "YurtArea";
    public float interactionDistance = 2.5f;

    private bool isLoading = false;

    void Update()
    {
        if (player == null || isLoading)
            return;

        Vector3 portalPosition = transform.position;
        Vector3 playerPosition = player.position;

        portalPosition.y = 0f;
        playerPosition.y = 0f;

        float distance = Vector3.Distance(
            portalPosition,
            playerPosition
        );

        if (distance > interactionDistance)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            isLoading = true;
            SceneManager.LoadScene(targetScene);
        }
    }
}
