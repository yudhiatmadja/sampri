using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace LMNT
{
    public class LMNTSpeechComponent
    {
        private string apiKey;
        private string voiceId;
        private const string API_BASE_URL = "https://api.lmnt.com/v1/ai/speech";
        private const int MAX_RETRIES = 3;

        // Constructor with voice ID parameter
        public LMNTSpeechComponent(string apiKey, string voiceId)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key cannot be empty");
            }
            this.apiKey = apiKey;
            this.voiceId = voiceId;
        }

        // Backward compatibility constructor
        public LMNTSpeechComponent(string apiKey) : this(apiKey, "3de2b1eb-ace0-40e5-99b5-69522bf53a50")
        {
            // Uses the default voice ID
        }

        public async Task<AudioClip> Speak(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text cannot be empty");
            }

            int retryCount = 0;
            while (retryCount < MAX_RETRIES)
            {
                try
                {
                    return await GenerateSpeech(text);
                }
                catch (Exception e)
                {
                    retryCount++;
                    if (retryCount >= MAX_RETRIES)
                    {
                        Debug.LogError($"Failed to generate speech after {MAX_RETRIES} attempts: {e.Message}");
                        throw;
                    }
                    Debug.LogWarning($"Retry {retryCount}/{MAX_RETRIES} after error: {e.Message}");
                    await Task.Delay(1000); // Wait before retry
                }
            }

            throw new Exception($"Failed to generate speech after {MAX_RETRIES} attempts");
        }

        private async Task<AudioClip> GenerateSpeech(string text)
        {
            // Create the JSON payload with the specified parameters
            string jsonData = JsonUtility.ToJson(new RequestWrapper
            {
                text = text,
                voice = voiceId,
                model = "blizzard",
                language = "id",
                format = "wav",
                sample_rate = 24000,
                speed = 1f,
                conversational = false,
                top_p = 1f,
                temperature = 1f,
                return_durations = false
            });

            Debug.Log($"Sending request to LMNT with API key: {apiKey}");
            Debug.Log($"Request payload: {jsonData}");

            using (UnityWebRequest request = new UnityWebRequest(API_BASE_URL, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // PERBAIKAN UTAMA: Mengubah format header autentikasi
                // Dari "Authorization: Bearer API_KEY"
                // Menjadi "X-API-Key: API_KEY" sesuai dengan curl command
                request.SetRequestHeader("X-API-Key", apiKey);

                // Send the request
                var operation = request.SendWebRequest();

                // Wait for the request to complete
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                // Log header response untuk debugging
                Debug.Log($"Response code: {request.responseCode}");

                // Check for errors
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Request failed: {request.error}");
                    Debug.LogError($"Response code: {request.responseCode}");

                    // Print Headers
                    Dictionary<string, string> headers = request.GetResponseHeaders();
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            Debug.LogError($"Header {header.Key}: {header.Value}");
                        }
                    }

                    Debug.LogError($"Response body: {request.downloadHandler.text}");
                    throw new Exception($"LMNT API Error: {request.error} - {request.downloadHandler.text}");
                }

                // Process successful response
                byte[] audioData = request.downloadHandler.data;

                Debug.Log($"Received audio data from LMNT API: {audioData.Length} bytes");

                // Convert to WAV AudioClip
                return await Task.Run(() => {
                    try
                    {
                        return ToAudioClip(audioData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to convert audio data: {e.Message}");
                        throw;
                    }
                });
            }
        }

        private AudioClip ToAudioClip(byte[] audioData)
        {
            // This is a simplified example - in real implementation, you would parse the WAV format properly
            WAVData wavData = ParseWAV(audioData);

            AudioClip audioClip = AudioClip.Create(
                "LMNTSpeech",
                wavData.SampleCount,
                wavData.ChannelCount,
                wavData.SampleRate,
                false);

            audioClip.SetData(wavData.AudioData, 0);
            return audioClip;
        }

        private WAVData ParseWAV(byte[] wavBytes)
        {
            try
            {
                // Ensure we have at least the RIFF header (44 bytes)
                if (wavBytes.Length < 44)
                {
                    throw new Exception($"WAV data too short: {wavBytes.Length} bytes");
                }

                // Validate RIFF header
                if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F')
                {
                    throw new Exception("Invalid WAV format: RIFF header not found");
                }

                // Read header information
                int channels = BitConverter.ToInt16(wavBytes, 22);
                int sampleRate = BitConverter.ToInt32(wavBytes, 24);
                int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

                Debug.Log($"WAV format: {channels} channels, {sampleRate} Hz, {bitsPerSample} bits per sample");

                // Find data chunk
                int dataIndex = 0;
                for (int i = 36; i < wavBytes.Length - 4; i++)
                {
                    if (wavBytes[i] == 'd' && wavBytes[i + 1] == 'a' && wavBytes[i + 2] == 't' && wavBytes[i + 3] == 'a')
                    {
                        dataIndex = i + 8; // Skip "data" and chunk size
                        break;
                    }
                }

                if (dataIndex == 0)
                {
                    throw new Exception("Invalid WAV format: data chunk not found");
                }

                Debug.Log($"Data chunk found at index {dataIndex}");

                // Calculate sample count
                int dataSize = wavBytes.Length - dataIndex;
                int bytesPerSample = bitsPerSample / 8;
                int sampleCount = dataSize / (bytesPerSample * channels);

                Debug.Log($"Sample count: {sampleCount}, data size: {dataSize} bytes");

                // Convert to float array for Unity AudioClip
                float[] audioData = new float[sampleCount * channels];
                int bytesPerFrame = bytesPerSample * channels;

                for (int i = 0; i < sampleCount; i++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        int sampleIndex = i * channels + c;
                        int byteIndex = dataIndex + (i * bytesPerFrame) + (c * bytesPerSample);

                        // Make sure we don't read past the end of the buffer
                        if (byteIndex + bytesPerSample > wavBytes.Length)
                        {
                            Debug.LogWarning($"Reading past end of buffer at sample {i}/{sampleCount}, channel {c}/{channels}");
                            continue;
                        }

                        // Convert based on bit depth
                        if (bitsPerSample == 16)
                        {
                            // 16-bit audio
                            short sample = BitConverter.ToInt16(wavBytes, byteIndex);
                            audioData[sampleIndex] = sample / 32768f; // Convert 16-bit to float [-1.0, 1.0]
                        }
                        else if (bitsPerSample == 8)
                        {
                            // 8-bit audio
                            audioData[sampleIndex] = (wavBytes[byteIndex] - 128) / 128f;
                        }
                        else if (bitsPerSample == 24)
                        {
                            // 24-bit audio (less common)
                            int sample = (wavBytes[byteIndex + 2] << 16) | (wavBytes[byteIndex + 1] << 8) | wavBytes[byteIndex];
                            // Handle sign extension for 24-bit values
                            if ((sample & 0x800000) != 0)
                                sample |= ~0xFFFFFF;
                            audioData[sampleIndex] = sample / 8388608f; // Convert 24-bit to float [-1.0, 1.0]
                        }
                        else if (bitsPerSample == 32)
                        {
                            // 32-bit audio (could be int or float)
                            // Assuming int for simplicity
                            int sample = BitConverter.ToInt32(wavBytes, byteIndex);
                            audioData[sampleIndex] = sample / 2147483648f; // Convert 32-bit to float [-1.0, 1.0]
                        }
                    }
                }

                return new WAVData
                {
                    ChannelCount = channels,
                    SampleRate = sampleRate,
                    SampleCount = sampleCount,
                    AudioData = audioData
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing WAV data: {e.Message}");
                throw;
            }
        }

        private class WAVData
        {
            public int ChannelCount;
            public int SampleRate;
            public int SampleCount;
            public float[] AudioData;
        }

        [Serializable]
        private class RequestWrapper
        {
            public string text;
            public string voice;
            public string model;
            public string language;
            public string format;
            public int sample_rate;
            public float speed;
            public bool conversational;
            public float top_p;
            public float temperature;
            public bool return_durations;
        }
    }
}