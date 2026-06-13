using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Seeman
{
    /// <summary>
    /// Plays guide / settings narration with a deep narrator voice (server TTS "narrator")
    /// plus a black band telop, 90s instructional-video style.
    /// </summary>
    public class NarratorSystem : MonoBehaviour
    {
        [HideInInspector] public AudioSource Voice;       // on camera, assigned by scene builder
        [HideInInspector] public GameObject Band;         // black band UI, assigned by GameUI
        [HideInInspector] public Text BandText;

        public bool IsPlaying { get; private set; }
        bool _skipRequested;

        static readonly string[] GuideLines =
        {
            "ようこそ。これは、ひとりの男と、一匹の魚の物語である。",
            "薄暗い水槽の中で、彼は今日も、あなたを待っている。…いや、待ってなどいない、と本人は言うだろう。",
            "画面をクリックするか、マイクに向かって声をかければ、彼はガラスの前までやって来る。",
            "「はなす」ボタンを押している間、あなたの声は彼に届く。指を離せば、彼は考え、口を開く。",
            "「エサ」ボタンで餌をやれば、機嫌は多少マシになる。腹を空かせたままにすれば…どうなるかは、想像に任せる。",
            "彼の性格は、あなたとの対話で少しずつ変わっていく。ただし、あなたの思う方向には、決して変わらない。",
            "それでは、良き水槽ライフを。…彼の皮肉に、あまり腹を立てないように。",
        };

        public void RequestSkip() { _skipRequested = true; }

        public void PlayGuide()
        {
            if (!IsPlaying) StartCoroutine(GuideRoutine());
        }

        IEnumerator GuideRoutine()
        {
            IsPlaying = true;
            _skipRequested = false;
            var G = GameManager.I;
            if (G != null) G.SetState(GameState.Guide);
            if (Band != null) Band.SetActive(true);

            for (int i = 0; i < GuideLines.Length; i++)
            {
                if (_skipRequested) break;
                yield return PlayLine(GuideLines[i], "guide_" + i.ToString("00"));
                yield return new WaitForSeconds(0.35f);
            }

            if (Band != null) Band.SetActive(false);
            if (G != null) G.SetState(GameState.Idle);
            IsPlaying = false;
        }

        public IEnumerator PlayLine(string line, string clipName = null)
        {
            var G = GameManager.I;

            // 1st choice: pre-generated narration (Irodori-TTS) bundled in Resources
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(clipName))
                clip = Resources.Load<AudioClip>("Narration/" + clipName);

            // 2nd choice: runtime TTS via the sidecar server
            if (clip == null && G != null && G.AI != null && G.AI.ServerOnline)
            {
                byte[] wav = null;
                yield return G.AI.Tts(line, "narrator", w => wav = w);
                if (wav != null) clip = WavUtility.ToAudioClip(wav, "narration");
            }

            float duration = Mathf.Clamp(line.Length * 0.135f, 2f, 14f);
            if (clip != null)
            {
                duration = clip.length;
                if (Voice != null) { Voice.clip = clip; Voice.Play(); }
            }

            if (BandText != null)
            {
                BandText.text = "";
                float reveal = Mathf.Min(line.Length * 0.05f, duration * 0.5f);
                float t = 0f;
                while (t < reveal && !_skipRequested)
                {
                    t += Time.deltaTime;
                    int n = Mathf.Clamp(Mathf.RoundToInt(line.Length * (t / reveal)), 0, line.Length);
                    BandText.text = line.Substring(0, n);
                    yield return null;
                }
                BandText.text = line;
                float remain = duration - reveal + 0.6f;
                float w = 0f;
                while (w < remain && !_skipRequested) { w += Time.deltaTime; yield return null; }
            }
            else
            {
                float w = 0f;
                while (w < duration && !_skipRequested) { w += Time.deltaTime; yield return null; }
            }
            if (Voice != null && _skipRequested) Voice.Stop();
        }
    }
}
