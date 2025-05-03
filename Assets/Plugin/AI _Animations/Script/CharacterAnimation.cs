using UnityEngine;
using System.Collections.Generic;

public class CharacterAnimation : MonoBehaviour
{
    private Animator animator;
    private Dictionary<string, int> questionCount = new Dictionary<string, int>();
    private bool hasWaved = false;
    private float lastInteractionTime;

    public float idleThreshold = 5f; // Idle2 setelah 5 detik tidak ada interaksi
    public int repeatThreshold = 3; // Complain jika pertanyaan diulang > 3 kali
    public string[] badWords = { "bodoh", "tolol", "goblok", "sialan", "anjing" };

    void Start()
    {
        animator = GetComponent<Animator>();
        lastInteractionTime = Time.time;

        // Jalankan animasi Wave hanya sekali
        if (!hasWaved)
        {
            animator.SetTrigger("DoWave");
            hasWaved = true;
        }
    }

    void Update()
    {
        float idleTime = Time.time - lastInteractionTime;

        if (idleTime >= idleThreshold)
        {
            animator.SetFloat("Idle", 1f); // Idle2 jika lama diam
        }
    }

    public void HandleAIResponse(string response)
    {
        ResetIdleTimer();
        
        int textLength = response.Length;
        if (textLength < 50)
        {
            animator.SetFloat("Talk", 0.5f); // Talk1
        }
        else
        {
            animator.SetFloat("Talk", 1f); // Talk2
        }

        animator.SetBool("IsTalking", true);
        Invoke(nameof(ResetToIdle), textLength * 0.05f);
    }

    private void ResetToIdle()
    {
        animator.SetBool("IsTalking", false);
        animator.SetFloat("Idle", 0f); // Kembali ke Idle1
    }

    public void CheckRepeatedQuestion(string question)
    {
        ResetIdleTimer();

        if (questionCount.ContainsKey(question))
            questionCount[question]++;
        else
            questionCount[question] = 1;

        if (questionCount[question] > repeatThreshold)
        {
            animator.SetTrigger("DoComplain");
            return;
        }

        foreach (string word in badWords)
        {
            if (question.ToLower().Contains(word))
            {
                animator.SetTrigger("DoAngry");
                return;
            }
        }
    }

    public void ResetIdleTimer()
    {
        lastInteractionTime = Time.time;
        animator.SetFloat("Idle", 0f); // Kembali ke Idle1
    }
}
