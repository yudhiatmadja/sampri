using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System;

public class OllamaChat : MonoBehaviour
{
    [Header("Ollama Settings")]
    [SerializeField] private string apiUrl = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "sampri-custom:latest";
    [SerializeField] private float responseTimeout = 30f; // Timeout in seconds

    [Header("Components")]
    [SerializeField] private LMNTAudioPlayer audioPlayer;
    [SerializeField] private Animation animationController; // Reference to custom Animation class

    private bool isProcessing = false;

    void Start()
    {
        // Try to find Animation controller if not assigned
        if (animationController == null)
        {
            animationController = GetComponent<Animation>();
            if (animationController == null)
            {
                animationController = FindAnyObjectByType<Animation>();
                if (animationController != null)
                {
                    Debug.Log("Animation controller ditemukan secara otomatis");
                }
                else
                {
                    Debug.LogWarning("Animation controller tidak ditemukan. Karakter tidak akan beranimasi.");
                }
            }
        }

        // Try to find LMNTAudioPlayer if not assigned
        if (audioPlayer == null)
        {
            audioPlayer = GetComponent<LMNTAudioPlayer>();
            if (audioPlayer == null)
            {
                audioPlayer = FindAnyObjectByType<LMNTAudioPlayer>();
                if (audioPlayer != null)
                {
                    Debug.Log("LMNTAudioPlayer ditemukan secara otomatis");
                }
                else
                {
                    Debug.LogError("LMNTAudioPlayer tidak ditemukan. Silakan pasang secara manual di Inspector");
                }
            }
        }

        // Subscribe to audio playback complete event
        if (audioPlayer != null)
        {
            audioPlayer.OnAudioPlaybackComplete += HandleAudioPlaybackComplete;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (audioPlayer != null)
        {
            audioPlayer.OnAudioPlaybackComplete -= HandleAudioPlaybackComplete;
        }
    }

    private void HandleAudioPlaybackComplete()
    {
        // Return to idle animation when audio playback completes
        if (animationController != null)
        {
            animationController.ReturnToIdle();
            Debug.Log("Animation: Returning to idle animation after audio playback");
        }
    }

    public void SendMessageToOllama(string userInput)
    {
        if (string.IsNullOrEmpty(userInput))
        {
            Debug.LogWarning("User input kosong, tidak ada yang dikirim");
            return;
        }

        if (isProcessing)
        {
            Debug.LogWarning("Masih memproses permintaan sebelumnya");
            return;
        }

        isProcessing = true;

        // Play wave animation when user sends a message
        if (animationController != null)
        {
            animationController.PlayWaveAnimation();
        }

        StartCoroutine(SendRequest(userInput));
    }

    private IEnumerator SendRequest(string userInput)
    {
        // Escape quotes and newlines in user input
        userInput = userInput.Replace("\"", "\\\"").Replace("\n", "\\n");

        string jsonBody = "{\"model\":\"" + modelName + "\",\"prompt\":\"" + userInput + "\",\"stream\":true}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // Set timeout 
            request.timeout = Mathf.RoundToInt(responseTimeout);

            // Send request and handle timeout
            float startTime = Time.time;
            request.SendWebRequest();

            while (!request.isDone)
            {
                if (Time.time - startTime > responseTimeout)
                {
                    request.Abort();
                    Debug.LogError("Request timeout after " + responseTimeout + " seconds");
                    isProcessing = false;

                    // Return to idle if timeout occurs
                    if (animationController != null)
                    {
                        animationController.ReturnToIdle();
                    }

                    yield break;
                }
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                yield return StartCoroutine(HandleStreamingResponse(request.downloadHandler.text));
            }
            else
            {
                Debug.LogError("Error: " + request.error);
                isProcessing = false;

                // Return to idle if request fails
                if (animationController != null)
                {
                    animationController.ReturnToIdle();
                }
            }
        }
    }

    private IEnumerator HandleStreamingResponse(string responseText)
    {
        StringBuilder fullResponse = new StringBuilder();
        string[] responseLines = responseText.Split('\n');

        foreach (string line in responseLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                OllamaResponse responseChunk = JsonUtility.FromJson<OllamaResponse>(line);
                if (responseChunk != null)
                {
                    fullResponse.Append(responseChunk.response);

                    // Update UI incrementally if needed
                    UpdateUIIncrementally(fullResponse.ToString());

                    if (responseChunk.done)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Gagal parse JSON: " + line + "\nError: " + e.Message);
            }

            yield return null;
        }

        string finalText = fullResponse.ToString().Trim();
        Debug.Log("Final Response: " + finalText);

        if (!string.IsNullOrEmpty(finalText))
        {
            // Animasi talking jalan
            if (animationController != null)
            {
                animationController.PlayTalkingAnimation();
                Debug.Log("Animation: Playing talking animation");
            }

            // TTS mulai bacain teks dari Ollama
            if (audioPlayer != null)
            {
                audioPlayer.PlayText(finalText);
                Debug.Log("Audio: Playing speech from text");
            }
            else
            {
                Debug.LogError("LMNTAudioPlayer tidak ditemukan!");

                // Return to idle if no audio player found
                if (animationController != null)
                {
                    animationController.ReturnToIdle();
                }
            }

            // Update UI dengan response final
            UpdateUIWithFinalResponse(finalText);
        }
        else
        {
            Debug.LogWarning("Response kosong dari Ollama");

            // Return to idle if empty response
            if (animationController != null)
            {
                animationController.ReturnToIdle();
            }
        }

        isProcessing = false;
    }

    private void UpdateUIIncrementally(string currentText)
    {
        // Find ChatUI component in scene
        ChatUI chatUI = FindAnyObjectByType<ChatUI>();
        if (chatUI != null)
        {
            try
            {
                System.Reflection.MethodInfo method = chatUI.GetType().GetMethod("UpdateResponseInProgress");
                if (method != null)
                {
                    method.Invoke(chatUI, new object[] { currentText });
                }
            }
            catch (Exception)
            {
                // Silent fail - method might not exist
            }
        }
    }

    private void UpdateUIWithFinalResponse(string finalText)
    {
        ChatUI chatUI = FindAnyObjectByType<ChatUI>();
        if (chatUI != null)
        {
            try
            {
                chatUI.HandleFinalResponse(finalText);
            }
            catch (Exception e)
            {
                Debug.LogError("Error updating UI: " + e.Message);
            }
        }
    }

    [System.Serializable]
    private class OllamaResponse
    {
        public string response;
        public bool done;
    }
}