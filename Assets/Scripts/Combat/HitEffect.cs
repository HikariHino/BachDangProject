using System.Collections;
using UnityEngine;

public class HitEffect : MonoBehaviour
{
    public GameObject hitPrefab;
    public AudioClip hitSound;
    public AudioClip swingSound; // Để dành cho đòn vung, có thể để null.

    [Header("Camera Shake")]
    [Tooltip("Magnitude of camera shake when hit")]
    public float shakeMagnitude = 0.05f;
    [Tooltip("Duration of camera shake (seconds)")]
    public float shakeTime = 0.1f;

    public void PlayHit(Vector3 pos)
    {
        StartCoroutine(PlayHitVisual(pos));
        PlayHitSound(pos);
        StartCoroutine(ShakeCamera());
    }

    // Hiệu ứng trúng đòn: dùng prefab nếu có, fallback quả cầu đỏ tự tạo.
    public IEnumerator PlayHitVisual(Vector3 pos)
    {
        if (hitPrefab != null)
        {
            GameObject effect = Instantiate(hitPrefab, pos, Quaternion.identity);
            Destroy(effect, 2f);
        }
        else
        {
            // Fallback: quả cầu nhỏ màu đỏ, không collider, to dần + mờ dần rồi hủy.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "HitEffectSphere";
            sphere.transform.position = pos;
            sphere.transform.localScale = Vector3.one * 0.25f;

            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
            {
                DestroyImmediate(sphereCollider);
            }

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            Material sphereMaterial = new Material(Shader.Find("Standard"));
            sphereMaterial.color = Color.red;
            // Chuyển sang transparent để fade alpha.
            sphereMaterial.SetFloat("_Mode", 3f);
            sphereMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sphereMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            sphereMaterial.SetInt("_ZWrite", 0);
            sphereMaterial.DisableKeyword("_ALPHATEST_ON");
            sphereMaterial.EnableKeyword("_ALPHABLEND_ON");
            sphereMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            sphereMaterial.renderQueue = 3000;
            sphereRenderer.material = sphereMaterial;

            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (sphere == null)
                {
                    yield break;
                }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                sphere.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.5f, t);
                Color c = sphereMaterial.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sphereMaterial.color = c;
                yield return null;
            }

            if (sphere != null)
            {
                Destroy(sphere);
            }
        }
    }

    private void PlayHitSound(Vector3 pos)
    {
        if (hitSound != null)
        {
            AudioSource source = GetComponent<AudioSource>();
            if (source != null)
            {
                source.PlayOneShot(hitSound);
            }
            else if (Camera.main != null)
            {
                AudioSource cameraSource = Camera.main.GetComponent<AudioSource>();
                if (cameraSource != null)
                {
                    cameraSource.PlayOneShot(hitSound);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(hitSound, pos);
                }
            }
        }
    }

    private IEnumerator ShakeCamera()
    {
        if (Camera.main == null)
            yield break;

        Vector3 originalPos = Camera.main.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeTime)
        {
            elapsed += Time.deltaTime;
            float percentComplete = elapsed / shakeTime;
            float damper = 1f - Mathf.Clamp01(percentComplete * 4f); // fade out shake
            float x = Random.value * 2f - 1f;
            float y = Random.value * 2f - 1f;
            x *= shakeMagnitude * damper;
            y *= shakeMagnitude * damper;

            Camera.main.transform.localPosition = new Vector3(x, y, originalPos.z);

            yield return null;
        }

        Camera.main.transform.localPosition = originalPos;
    }}
