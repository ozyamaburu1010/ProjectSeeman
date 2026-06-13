using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Seeman
{
    public class ConversationManager : MonoBehaviour
    {
        public int MaxHistory = 12;

        readonly List<ChatMessage> _history = new List<ChatMessage>();
        bool _busy;
        bool _interrupt;
        float _nextMutter;
        float _nextProactive;

        GameManager G => GameManager.I;

        void Start()
        {
            _nextMutter = Time.time + Random.Range(20f, 40f);
            _nextProactive = Time.time + Random.Range(50f, 100f);
            if (G != null && G.Mic != null)
                G.Mic.OnVoiceDetected += () => { if (G.Fish != null) G.Fish.Summon(); };
            StartCoroutine(HealthLoop());
        }

        IEnumerator HealthLoop()
        {
            while (true)
            {
                if (G != null && G.AI != null) yield return G.AI.CheckHealth();
                if (G != null && G.UI != null) G.UI.UpdateServerStatus();
                yield return new WaitForSeconds(6f);
            }
        }

        void Update()
        {
            if (G == null || _busy || G.State != GameState.Idle) return;

            // the fish sometimes starts the conversation on its own
            if (Time.time >= _nextProactive)
            {
                _nextProactive = Time.time + Random.Range(120f, 260f);
                _nextMutter = Time.time + Random.Range(35f, 80f);
                StartCoroutine(ProactiveCall());
                return;
            }

            if (Time.time >= _nextMutter)
            {
                _nextMutter = Time.time + Random.Range(35f, 80f);
                string line = CannedResponses.PickMutter();
                if (G.UI != null) G.UI.Telop.Show("シーマン", line, 4.5f);
            }
        }

        IEnumerator ProactiveCall()
        {
            _busy = true;
            _interrupt = false;
            if (G.Fish != null) G.Fish.Summon();

            // wait until the fish reaches the glass (or give up after 8s)
            float t = 0f;
            while (t < 8f && G.Fish != null && G.Fish.State == FishState.Approach)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (G.Fish != null) G.Fish.OnConversationStart();

            string line = null;
            byte[] wav = null;
            string name = G.Personality != null ? G.Personality.Data.userName : "";

            if (G.AI != null && G.AI.ServerOnline)
            {
                var messages = new List<ChatMessage>
                {
                    new ChatMessage { role = "system", content = G.Personality.BuildSystemPrompt() },
                    new ChatMessage { role = "user", content =
                        "(システム指示: 今、飼い主が水槽の前を通りかかった。お前の方から短く声をかけろ。" +
                        (string.IsNullOrEmpty(name) ? "" : "名前を呼んでやれ。") +
                        "挨拶でも皮肉でも雑談の振りでもいい。1〜2文。)" }
                };
                yield return G.AI.Chat(messages, r => line = r);
                if (!string.IsNullOrEmpty(line))
                    yield return G.AI.Tts(line, "seeman", w => wav = w);
            }

            if (string.IsNullOrEmpty(line)) line = CannedResponses.PickCallOut(name);

            yield return Speak(line, wav);
            _busy = false;
            G.SetState(GameState.Idle);
        }

        static readonly System.Text.RegularExpressions.Regex NameRegex =
            new System.Text.RegularExpressions.Regex(
                "(?:私|俺|僕|わたし|あたし|ぼく|おれ)(?:の名前)?は([\\p{L}\\p{N}ー]{1,12}?)(?:だよ|だ|です|と(?:いう|言う|申す)|って(?:いう|言う)よ?)");

        // ---- push-to-talk from UI ----
        public void OnMicPressed()
        {
            if (G == null) return;
            if (G.Mic == null || !G.Mic.HasMic)
            {
                if (G.UI != null) G.UI.Telop.Show("システム", "マイクが見つからない。…まあ、黙って眺めるのも一興だがね。", 4f);
                return;
            }
            // barge-in: pressing the talk button cuts off whatever the fish is saying
            if (_busy)
            {
                _interrupt = true;
                var src = G.Fish != null ? G.Fish.GetComponent<AudioSource>() : null;
                if (src != null) src.Stop();
            }
            if (G.Fish != null) G.Fish.Summon();
            G.Mic.StartRecording();
            G.SetState(GameState.Listening);
        }

        public void OnMicReleased()
        {
            if (G == null || G.Mic == null || !G.Mic.IsRecording) return;
            var clip = G.Mic.StopRecording();
            if (clip == null)
            {
                G.SetState(GameState.Idle);
                if (G.UI != null) G.UI.Telop.Show("シーマン", "…なんだ、声が短すぎて聞こえんよ。", 3f);
                return;
            }
            StartCoroutine(WaitThenRunExchange(clip));
        }

        IEnumerator WaitThenRunExchange(AudioClip clip)
        {
            // let an interrupted coroutine wind down so _busy doesn't collide
            float t = 0f;
            while (_busy && t < 2f) { t += Time.deltaTime; yield return null; }
            yield return RunExchange(clip);
        }

        IEnumerator RunExchange(AudioClip recorded)
        {
            _busy = true;
            _interrupt = false;
            G.SetState(GameState.Thinking);
            if (G.Fish != null) G.Fish.OnConversationStart();

            string userText = null;
            string reply = null;
            byte[] ttsWav = null;
            bool online = G.AI != null && G.AI.ServerOnline;

            if (online)
            {
                byte[] wav = WavUtility.FromAudioClip(recorded);
                yield return G.AI.Stt(wav, t => userText = t);

                if (!string.IsNullOrWhiteSpace(userText))
                {
                    if (G.UI != null) G.UI.Telop.Show("あなた", userText, 3f);

                    // learn the user's name when they introduce themselves
                    var nm = NameRegex.Match(userText);
                    if (nm.Success && G.Personality != null && string.IsNullOrEmpty(G.Personality.Data.userName))
                    {
                        G.Personality.Data.userName = nm.Groups[1].Value;
                        G.Personality.Save();
                    }

                    var messages = new List<ChatMessage>
                    {
                        new ChatMessage { role = "system", content = G.Personality.BuildSystemPrompt() }
                    };
                    messages.AddRange(_history);
                    messages.Add(new ChatMessage { role = "user", content = userText });

                    yield return G.AI.Chat(messages, r => reply = r);

                    if (!string.IsNullOrEmpty(reply))
                    {
                        _history.Add(new ChatMessage { role = "user", content = userText });
                        _history.Add(new ChatMessage { role = "assistant", content = reply });
                        while (_history.Count > MaxHistory) _history.RemoveAt(0);
                        yield return G.AI.Tts(reply, "seeman", w => ttsWav = w);
                    }
                }
                else
                {
                    reply = "…もごもご言われても聞き取れんね。水の中なんだ、はっきり喋れ。";
                }
            }

            if (string.IsNullOrEmpty(reply)) reply = CannedResponses.PickOffline();

            G.Personality.RegisterExchange(userText, reply);
            yield return Speak(reply, ttsWav);

            _busy = false;
            G.SetState(GameState.Idle);
        }

        public IEnumerator Speak(string text, byte[] wav)
        {
            G.SetState(GameState.Speaking);
            var fish = G.Fish;
            var lip = fish != null ? fish.GetComponent<FishLipSync>() : null;
            var src = fish != null ? fish.GetComponent<AudioSource>() : null;

            float duration = Mathf.Clamp(text.Length * 0.13f, 1.5f, 12f);
            AudioClip clip = wav != null ? WavUtility.ToAudioClip(wav, "tts") : null;
            if (clip != null) duration = clip.length;

            if (G.UI != null) G.UI.Telop.Show("シーマン", text, duration + 1.2f);

            if (clip != null && src != null)
            {
                src.clip = clip;
                src.Play();
            }
            else if (lip != null)
            {
                lip.FakeTalk(duration);
            }
            float t = 0f;
            while (t < duration && !_interrupt)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (_interrupt && src != null) src.Stop();
            if (lip != null) lip.StopTalk();
        }

        /// <summary>Make the fish comment without user input (e.g. after eating).</summary>
        public void FishComment(string text)
        {
            if (_busy) return;
            StartCoroutine(CommentRoutine(text));
        }

        IEnumerator CommentRoutine(string text)
        {
            _busy = true;
            _interrupt = false;
            byte[] wav = null;
            if (G.AI != null && G.AI.ServerOnline)
                yield return G.AI.Tts(text, "seeman", w => wav = w);
            yield return Speak(text, wav);
            _busy = false;
            G.SetState(GameState.Idle);
        }
    }
}
