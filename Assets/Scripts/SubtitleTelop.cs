using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Seeman
{
    public class SubtitleTelop : MonoBehaviour
    {
        [HideInInspector] public Text Label;       // assigned by GameUI at runtime
        [HideInInspector] public Text SpeakerLabel;

        Coroutine _routine;

        public void Show(string speaker, string text, float holdSeconds)
        {
            if (Label == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Reveal(speaker, text, holdSeconds));
        }

        IEnumerator Reveal(string speaker, string text, float hold)
        {
            if (SpeakerLabel != null)
                SpeakerLabel.text = string.IsNullOrEmpty(speaker) ? "" : "【" + speaker + "】";
            Label.text = "";

            float revealTime = Mathf.Min(text.Length * 0.045f, hold * 0.55f);
            float t = 0f;
            while (t < revealTime)
            {
                t += Time.deltaTime;
                int n = Mathf.Clamp(Mathf.RoundToInt(text.Length * (t / revealTime)), 0, text.Length);
                Label.text = text.Substring(0, n);
                yield return null;
            }
            Label.text = text;
            yield return new WaitForSeconds(Mathf.Max(0.5f, hold - revealTime));
            Label.text = "";
            if (SpeakerLabel != null) SpeakerLabel.text = "";
            _routine = null;
        }
    }
}
