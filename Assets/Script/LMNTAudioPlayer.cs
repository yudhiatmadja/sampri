using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using LMNT;

public class LMNTAudioPlayer : MonoBehaviour
{
    public AudioSource audioSource;
    [TextArea] public string textToSpeak;
    [Header("API Key dari LMNT.com")]
    public string apiKey = "6679fdcfa98742d5996f1302bc61f982";

    [Header("Voice ID")]
    public string voiceId = "3de2b1eb-ace0-40e5-99b5-69522bf53a50";

    private LMNTSpeechComponent speechComponent;
    private bool isProcessing = false;

    // Event to notify when audio playback is completed
    public delegate void AudioPlaybackComplete();
    public event AudioPlaybackComplete OnAudioPlaybackComplete;

    void Awake()
    {
        // Pastikan AudioSource ada
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("AudioSource ditambahkan secara otomatis");
            }
        }

        // Inisialisasi komponen TTS dengan API key
        InitializeSpeechComponent();
    }

    private void InitializeSpeechComponent()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API Key not set! Please enter a valid API key from LMNT.com");
            return;
        }

        try
        {
            speechComponent = new LMNTSpeechComponent(apiKey, voiceId);
            Debug.Log("LMNT Speech Component berhasil diinisialisasi dengan API key: " + apiKey);
            Debug.Log("Voice ID terdeteksi: " + voiceId);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal inisialisasi LMNT Speech Component: " + e.Message);
        }
    }

    void Start()
    {
        // Jalankan coroutine untuk TTS di awal jika ada teks
        if (!string.IsNullOrEmpty(textToSpeak))
        {
            StartCoroutine(PlaySpeechCoroutine());
        }
    }

    public IEnumerator PlaySpeechCoroutine()
    {
        if (string.IsNullOrEmpty(textToSpeak))
        {
            Debug.LogWarning("Text kosong bro.");
            yield break;
        }

        if (isProcessing)
        {
            Debug.LogWarning("Masih proses TTS sebelumnya, tunggu dulu.");
            yield break;
        }

        if (speechComponent == null)
        {
            Debug.LogError("LMNT Speech Component belum diinisialisasi dengan benar!");
            InitializeSpeechComponent();

            if (speechComponent == null)
            {
                Debug.LogError("Tetap gagal menginisialisasi Speech Component");
                yield break;
            }
        }

        isProcessing = true;
        Debug.Log("Memulai konversi teks ke suara: " + textToSpeak);

        Task<AudioClip> task = null;

        try
        {
            task = speechComponent.Speak(textToSpeak);
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Exception saat membuat permintaan LMNT: " + e.Message);
            isProcessing = false;
            yield break;
        }

        if (task == null)
        {
            Debug.LogError("❌ Task null setelah mencoba membuat permintaan LMNT");
            isProcessing = false;
            yield break;
        }

        // Add a timeout for the task (30 seconds)
        float startTime = Time.time;
        float timeout = 30f;

        // Wait for the task to complete
        while (!task.IsCompleted)
        {
            // Check for timeout
            if (Time.time - startTime > timeout)
            {
                Debug.LogError("Timeout saat meminta audio dari LMNT setelah " + timeout + " detik.");
                isProcessing = false;
                yield break;
            }

            // Check for exceptions during processing
            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    Debug.LogError("Error saat memproses audio dari LMNT: " + task.Exception.Message);
                    if (task.Exception.InnerException != null)
                    {
                        Debug.LogError("Inner Exception: " + task.Exception.InnerException.Message);
                    }
                }
                else
                {
                    Debug.LogError("Terjadi kesalahan saat memproses permintaan LMNT.");
                }
                isProcessing = false;
                yield break;
            }

            yield return null;
        }

        // Handle the completed task
        if (task.IsCompletedSuccessfully && task.Result != null)
        {
            // Stop audio sebelumnya jika masih playing
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            audioSource.clip = task.Result;
            audioSource.Play();
            Debug.Log("✅ Suara berhasil diputar! Panjang audio: " + audioSource.clip.length + " detik");

            // Tunggu sampai audio selesai diputar
            yield return new WaitForSeconds(audioSource.clip.length);

            // Trigger event when audio playback completes
            if (OnAudioPlaybackComplete != null)
            {
                OnAudioPlaybackComplete.Invoke();
            }
        }
        else
        {
            Debug.LogError("❌ Gagal dapat audio dari LMNT: Task selesai tetapi hasilnya null");
        }

        isProcessing = false;
    }

    // Method untuk dipanggil dari OllamaChat
    public void PlayText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("Text kosong, tidak bisa diputar.");
            return;
        }

        // Log untuk debugging
        Debug.Log("PlayText dipanggil dengan text: " + (text.Length > 50 ? text.Substring(0, 50) + "..." : text));

        // Verifikasi API key
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("API Key kosong. Pastikan Anda mendaftarkan API key valid dari LMNT.com");
        }

        // Verifikasi komponen LMNT
        if (speechComponent == null)
        {
            Debug.LogError("LMNT Speech Component null! Mencoba reinisialisasi...");
            InitializeSpeechComponent();

            if (speechComponent == null)
            {
                Debug.LogError("Gagal reinisialisasi LMNT Speech Component");
                return;
            }
        }

        textToSpeak = text;
        StopAllCoroutines(); // Hentikan coroutine sebelumnya jika masih berjalan
        StartCoroutine(PlaySpeechCoroutine());
    }

    // Method untuk menghentikan audio yang sedang berjalan
    public void StopAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        StopAllCoroutines();
        isProcessing = false;

        // Notify that audio playback has been stopped
        if (OnAudioPlaybackComplete != null)
        {
            OnAudioPlaybackComplete.Invoke();
        }
    }

    // Untuk debugging - tambahkan ke menu konteks
    [ContextMenu("Test LMNT Connection")]
    public void TestLMNTConnection()
    {
        StartCoroutine(TestConnectionCoroutine());
    }

    private IEnumerator TestConnectionCoroutine()
    {
        Debug.Log("Testing LMNT connection with API key: " + apiKey);
        Debug.Log("Using voice ID: " + voiceId);

        if (speechComponent == null)
        {
            InitializeSpeechComponent();

            if (speechComponent == null)
            {
                Debug.LogError("Failed to create LMNT Speech Component");
                yield break;
            }
        }

        string testText = "This is a test of the LMNT speech system.";
        Debug.Log("Requesting speech for test text: " + testText);

        Task<AudioClip> task = null;

        try
        {
            task = speechComponent.Speak(testText);
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ LMNT connection test failed with exception: " + e.Message);
            yield break;
        }

        if (task == null)
        {
            Debug.LogError("❌ LMNT returned null task");
            yield break;
        }

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsCompletedSuccessfully && task.Result != null)
        {
            Debug.Log("✅ LMNT connection successful! Received audio clip with length: " + task.Result.length);
        }
        else
        {
            Debug.LogError("❌ LMNT connection test failed: No audio clip received");
        }
    }
}