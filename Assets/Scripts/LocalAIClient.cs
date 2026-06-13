using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Seeman
{
    [Serializable] public class ChatMessage { public string role; public string content; }
    [Serializable] public class ChatRequest { public List<ChatMessage> messages; }
    [Serializable] public class ChatResponse { public string text; }
    [Serializable] public class SttResponse { public string text; }
    [Serializable] public class TtsRequest { public string text; public string voice; }

    [Serializable]
    public class HealthInfo
    {
        public string status;
        public string llm_model;
        public string tts_engine;
        public string stt_engine;
        public float vram_gb;
        public bool ollama_online;
    }

    public class LocalAIClient : MonoBehaviour
    {
        public string BaseUrl = "http://127.0.0.1:8765";
        public bool ServerOnline { get; private set; }
        public HealthInfo Health { get; private set; }

        public IEnumerator CheckHealth(Action<bool> done = null)
        {
            using (var req = UnityWebRequest.Get(BaseUrl + "/health"))
            {
                req.timeout = 3;
                yield return req.SendWebRequest();
                ServerOnline = req.result == UnityWebRequest.Result.Success;
                if (ServerOnline)
                {
                    try { Health = JsonUtility.FromJson<HealthInfo>(req.downloadHandler.text); }
                    catch { Health = null; }
                }
                done?.Invoke(ServerOnline);
            }
        }

        public IEnumerator Stt(byte[] wav, Action<string> done)
        {
            using (var req = new UnityWebRequest(BaseUrl + "/stt", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(wav) { contentType = "audio/wav" };
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 60;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    SttResponse r = null;
                    try { r = JsonUtility.FromJson<SttResponse>(req.downloadHandler.text); } catch { }
                    done?.Invoke(r != null ? r.text : null);
                }
                else done?.Invoke(null);
            }
        }

        public IEnumerator Chat(List<ChatMessage> messages, Action<string> done)
        {
            string body = JsonUtility.ToJson(new ChatRequest { messages = messages });
            using (var req = new UnityWebRequest(BaseUrl + "/chat", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 120;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    ChatResponse r = null;
                    try { r = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text); } catch { }
                    done?.Invoke(r != null && !string.IsNullOrEmpty(r.text) ? r.text : null);
                }
                else done?.Invoke(null);
            }
        }

        public IEnumerator Tts(string text, string voice, Action<byte[]> done)
        {
            string body = JsonUtility.ToJson(new TtsRequest { text = text, voice = voice });
            using (var req = new UnityWebRequest(BaseUrl + "/tts", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)) { contentType = "application/json" };
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 60;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.data != null && req.downloadHandler.data.Length > 44)
                    done?.Invoke(req.downloadHandler.data);
                else done?.Invoke(null);
            }
        }
    }
}
