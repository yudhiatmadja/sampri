using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class SpeechToText : MonoBehaviour
{
    public Button toggleButton;
    // public TMPro.TMP_Text resultText;
    public TMPro.TMP_InputField resultInputField;
    public Text buttonText;  // Ubah text button Start <-> Stop

    private AudioClip audioClip;
    private string micDevice;
    private const int sampleRate = 16000;
    private const string serverIp = "127.0.0.1";
    private const int serverPort = 5000;

    private bool isRecording = false;

    void Start()
    {
        toggleButton.onClick.AddListener(ToggleRecording);
        micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

        if (micDevice == null)
        {
            Debug.LogError("No microphone found!");
            toggleButton.interactable = false;
        }


    }

    void ToggleRecording()
    {
        if (isRecording)
        {
            StopRecordingAndSend();
        }
        else
        {
            StartRecording();
        }
        isRecording = !isRecording;
        buttonText.text = isRecording ? "Stop Recording" : "Start Recording";
    }

    void StartRecording()
    {
        audioClip = Microphone.Start(micDevice, false, 5, sampleRate);
        Debug.Log("Recording started...");
    }

    async void StopRecordingAndSend()
    {
        Microphone.End(micDevice);
        Debug.Log("Recording stopped.");

        byte[] wavData = ConvertClipToWav(audioClip);
        resultInputField.text = "Processing...";

        string transcription = await SendToWhisperAsync(wavData);
        //implementasi post processing
        string correctedTranscription = SpeechPostProcessing.CorrectNames(transcription);

        // resultText.text = transcription;
        resultInputField.text = correctedTranscription;

        Debug.Log("Transcription: " + transcription);
        Debug.Log("Setelah Post-Processing: " + correctedTranscription);
    }

    byte[] ConvertClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * 2);  // PCM 16bit
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // PCM format
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);

            foreach (float sample in samples)
            {
                short pcmSample = (short)(sample * 32767);
                writer.Write(pcmSample);
            }

            return stream.ToArray();
        }
    }

    async Task<string> SendToWhisperAsync(byte[] wavData)
    {
        try
        {
            using (var client = new TcpClient(serverIp, serverPort))
            using (var stream = client.GetStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(wavData.Length);
                writer.Write(wavData);
                writer.Flush();

                using (var reader = new StreamReader(stream))
                {
                    string response = await reader.ReadLineAsync();
                    return response ?? "No response from server.";
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error sending audio: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    // async Task<string> SendToWhisperAsync(byte[] wavData)
    // {
    //     string basePath = Directory.GetParent(Application.dataPath).FullName;
    //     string wavPath = Path.Combine(Application.persistentDataPath, "temp.wav");
    //     string txtPath = Path.Combine(Application.persistentDataPath, "output.txt");
    //     string exePath = @"C:\Game\Game_Project\stt-game\whisper.cpp-master\build\bin\Debug\main.exe";

    //     string modelPath = Path.Combine(basePath, "whisper.cpp-master", "models", "ggml-medium.bin");

    //     // Simpan file WAV
    //     await File.WriteAllBytesAsync(wavPath, wavData);

    //     // Jalankan Whisper
    //     ProcessStartInfo psi = new ProcessStartInfo
    //     {
    //         FileName = exePath,
    //         Arguments = $"-m \"{modelPath}\" -f \"{wavPath}\" -otxt -of \"{txtPath}\" -l id",
    //         UseShellExecute = false,
    //         RedirectStandardOutput = true,
    //         RedirectStandardError = true,
    //         CreateNoWindow = true
    //     };

    //     using (Process proc = Process.Start(psi))
    //     {
    //         string err = await proc.StandardError.ReadToEndAsync();  // Buat debugging kalau ada error
    //         string log = await proc.StandardOutput.ReadToEndAsync();
    //         Debug.Log("Whisper output: " + log);
    //         Debug.Log("Whisper error: " + err);
    //         await Task.Run(() => proc.WaitForExit());

    //     }

    //     // Ambil hasil transkripsi
    //     if (File.Exists(txtPath))
    //     {
    //         string transcription = await File.ReadAllTextAsync(txtPath);
    //         return transcription;
    //     }
    //     return "No transcription generated.";
    // }



// async Task<string> SendToWhisperAsync(byte[] wavData)
// {

// string exePath = @"C:/Game/Game_Project/stt-game/Assets/whisper.cpp-master/build/bin/Debug/whisper-cli.exe";

    
//     string basePath = Directory.GetParent(Application.dataPath).FullName;
//     string modelPath = @"C:/Game/Game_Project/stt-game/Assets/StreamingAssets/whisper.cpp/models/ggml-medium.bin";
//     string wavPath = Path.Combine(Application.persistentDataPath, "temp.wav");
//     var txtPath = Application.persistentDataPath + "/output.txt.txt";


//     Debug.Log($"Executable path: {exePath}");
//     Debug.Log($"Model path: {modelPath}");
//     Debug.Log($"WAV path: {wavPath}");
//     Debug.Log($"Output text path: {txtPath}");

//     if (!File.Exists(exePath))
//     {
//         Debug.LogError($"Executable not found at: {exePath}");
//         return "Error: Whisper executable not found.";
//     }

//     if (!File.Exists(modelPath))
//     {
//         Debug.LogError($"Model not found at: {modelPath}");
//         return "Error: Whisper model not found.";
//     }

//     await File.WriteAllBytesAsync(wavPath, wavData);
//     if (!File.Exists(wavPath))
//     {
//         Debug.LogError($"Failed to save WAV file at: {wavPath}");
//         return "Error: Failed to save audio file.";
//     }
//     Debug.Log($"WAV file saved: {wavPath}, size: {wavData.Length} bytes");

//     try
//     {
//         ProcessStartInfo psi = new ProcessStartInfo
//         {
//             FileName = exePath,
//             Arguments = $"-m \"{modelPath}\" -f \"{wavPath}\" -otxt -of \"{txtPath}\" -l id",
//             UseShellExecute = false,
//             RedirectStandardOutput = true,
//             RedirectStandardError = true,
//             CreateNoWindow = true,
//             WorkingDirectory = Path.GetDirectoryName(exePath) 
//         };

//         Debug.Log($"Running command: {exePath} {psi.Arguments}");

//         using (Process proc = Process.Start(psi))
//         {
//             string err = await proc.StandardError.ReadToEndAsync();
//             string log = await proc.StandardOutput.ReadToEndAsync();
            
//             Debug.Log("Whisper output: " + log);
//             if (!string.IsNullOrEmpty(err))
//             {
//                 Debug.LogError("Whisper error: " + err);
//             }
            
//             await Task.Run(() => proc.WaitForExit());
//             Debug.Log($"Whisper process exited with code: {proc.ExitCode}");
//         }

//         if (File.Exists(txtPath))
//         {
//             string transcription = await File.ReadAllTextAsync(txtPath);
//             Debug.Log($"Found output file at {txtPath}, content length: {transcription.Length}");
//             return transcription;
//         }
//         else
//         {
//             Debug.LogError($"Output file not found at: {txtPath}");
//             return "Error: Transcription file not generated.";
//         }
//     }
//     catch (System.Exception ex)
//     {
//         Debug.LogError($"Error running WhisperAI: {ex.Message}\nStack trace: {ex.StackTrace}");
//         return $"Error: {ex.Message}";
//     }
// }

}
