using System.Collections;
using UnityEngine;

public class GatePrompt : MonoBehaviour
{
    public Transform player;
    public Transform destination;
    public float distance = 30f;
    public float fadeDuration = 0.7f;

    private bool nearby;
    private bool transitioning;
    private float fadeAlpha;

    private void Update()
    {
        if (player == null || transitioning)
        {
            nearby = false;
            return;
        }

        float currentDistance = Vector3.Distance(transform.position, player.position);
        nearby = currentDistance <= distance;

        if (nearby && Input.GetKeyDown(KeyCode.E))
        {
            if (destination == null)
            {
                Debug.LogError("Chưa gán VillageArrivalPoint vào ô Destination.");
                return;
            }

            StartCoroutine(FadeAndTeleport());
        }
    }

    private IEnumerator FadeAndTeleport()
    {
        transitioning = true;

        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        yield return FadeTo(1f);

        var body = player.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = destination.position;
            body.rotation = destination.rotation;
        }
        else
        {
            player.SetPositionAndRotation(destination.position, destination.rotation);
        }

        Physics.SyncTransforms();

        yield return new WaitForSecondsRealtime(0.25f);
        yield return FadeTo(0f);

        Time.timeScale = previousTimeScale;
        transitioning = false;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        float startAlpha = fadeAlpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        fadeAlpha = targetAlpha;
    }

    private void OnGUI()
    {
        if (transitioning)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(
                new Rect(0, 0, Screen.width, Screen.height),
                Texture2D.whiteTexture
            );
            GUI.color = previousColor;
            return;
        }

        if (!nearby) return;

        GUIStyle promptStyle = new GUIStyle(GUI.skin.box);
        promptStyle.fontSize = 26;
        promptStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Box(
            new Rect(Screen.width / 2 - 230, Screen.height - 130, 460, 70),
            "Bấm E để qua dân làng bên sông",
            promptStyle
        );
    }
}