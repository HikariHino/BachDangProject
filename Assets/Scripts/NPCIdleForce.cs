using UnityEngine;

public class NPCIdleForce : MonoBehaviour
{
    void Start()
    {
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            anim.SetBool("isRunning", false);
            anim.Play("Idle");
        }
    }
}
