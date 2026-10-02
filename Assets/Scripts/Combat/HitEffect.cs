using UnityEngine;

public class HitEffect : MonoBehaviour
{
    public GameObject hitPrefab;
    public AudioClip hitSound;
    public AudioClip swingSound; // Để dành cho đòn vung, có thể để null.

    public void PlayHit(Vector3 pos)
    {
        if (hitPrefab != null)
        {
            GameObject effect = Instantiate(hitPrefab, pos, Quaternion.identity);
            Destroy(effect, 2f);
        }

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
}
