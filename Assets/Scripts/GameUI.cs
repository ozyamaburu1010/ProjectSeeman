using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Seeman
{
    /// <summary>Builds the whole retro-CRT UI at runtime (no prefabs needed).</summary>
    [DefaultExecutionOrder(-50)]
    public class GameUI : MonoBehaviour
    {
        [HideInInspector] public SubtitleTelop Telop;

        static readonly Color Green = new Color(0.55f, 1f, 0.62f);
        static readonly Color DimGreen = new Color(0.45f, 0.85f, 0.5f, 0.85f);
        static readonly Color PanelBg = new Color(0.02f, 0.06f, 0.03f, 0.94f);

        Font _font;
        Canvas _canvas;
        Text _osdLeft, _osdClock, _meters, _serverStatus, _micHint;
        GameObject _recDot;
        Text _recText;
        GameObject _settingsPanel;
        Text _statusReport;
        Button _mbtiButton;
        GameObject _narrBand;
        Text _narrText;
        int _mbtiIndex;

        static readonly string[] MbtiOptions =
        {
            "未設定", "INTJ", "INTP", "ENTJ", "ENTP", "INFJ", "INFP", "ENFJ", "ENFP",
            "ISTJ", "ISFJ", "ESTJ", "ESFJ", "ISTP", "ISFP", "ESTP", "ESFP"
        };

        void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildOsd();
            BuildTelop();
            BuildNarratorBand();
            BuildButtons();
            BuildSettingsPanel();
        }

        void Start()
        {
            var g = GameManager.I;
            if (g != null && g.Narrator != null)
            {
                g.Narrator.Band = _narrBand;
                g.Narrator.BandText = _narrText;
            }
            InvokeRepeating(nameof(UpdateMeters), 0.5f, 1f);
            // first-run guide
            if (PlayerPrefs.GetInt("seeman_guide_done", 0) == 0)
            {
                PlayerPrefs.SetInt("seeman_guide_done", 1);
                Invoke(nameof(AutoGuide), 2.5f);
            }
        }

        void AutoGuide()
        {
            var g = GameManager.I;
            if (g != null && g.Narrator != null) g.Narrator.PlayGuide();
        }

        // ---------- builders ----------

        void BuildCanvas()
        {
            var go = gameObject;
            _canvas = go.GetComponent<Canvas>();
            if (_canvas == null) _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = Camera.main;
            _canvas.planeDistance = 0.5f;
            var scaler = go.GetComponent<CanvasScaler>() ?? go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
            Telop = go.GetComponent<SubtitleTelop>() ?? go.AddComponent<SubtitleTelop>();
        }

        RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        Text NewText(string name, Transform parent, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0.25f, 0.05f, 0.9f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        Image NewPanel(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        Button NewButton(string name, Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewPanel(name, parent, new Color(0.03f, 0.1f, 0.05f, 0.9f));
            var rt = img.rectTransform;
            rt.sizeDelta = size;
            // without a LayoutElement, VerticalLayoutGroup collapses buttons to 0 height
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            le.minHeight = size.y;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.1f, 0.35f, 0.15f, 1f);
            colors.pressedColor = new Color(0.2f, 0.6f, 0.25f, 1f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = NewText("Label", rt, 30, Green, TextAnchor.MiddleCenter);
            Fill(t.rectTransform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.text = label;
            return btn;
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void BuildOsd()
        {
            _osdLeft = NewText("OsdChannel", transform, 36, Green);
            var rt = _osdLeft.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(55, -40);
            rt.sizeDelta = new Vector2(700, 50);
            _osdLeft.text = "CH 3   AQUARIUM";

            _osdClock = NewText("OsdClock", transform, 36, Green, TextAnchor.UpperRight);
            rt = _osdClock.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-55, -40);
            rt.sizeDelta = new Vector2(300, 50);

            _meters = NewText("Meters", transform, 26, DimGreen);
            rt = _meters.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(55, -95);
            rt.sizeDelta = new Vector2(700, 90);

            _serverStatus = NewText("ServerStatus", transform, 22, DimGreen);
            rt = _serverStatus.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(55, 30);
            rt.sizeDelta = new Vector2(900, 36);
            _serverStatus.text = "AI: 確認中…";

            // REC indicator
            var recImg = NewPanel("RecDot", transform, new Color(1f, 0.15f, 0.1f, 1f));
            rt = recImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-200, -100);
            rt.sizeDelta = new Vector2(24, 24);
            _recDot = recImg.gameObject;
            _recText = NewText("RecText", transform, 30, new Color(1f, 0.3f, 0.25f), TextAnchor.UpperRight);
            rt = _recText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-55, -98);
            rt.sizeDelta = new Vector2(130, 40);
            _recText.text = "REC";
            _recText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _recDot.SetActive(false);
            _recText.gameObject.SetActive(false);
        }

        void BuildTelop()
        {
            var speaker = NewText("TelopSpeaker", transform, 30, new Color(1f, 0.9f, 0.5f), TextAnchor.LowerLeft);
            var rt = speaker.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 215);
            rt.sizeDelta = new Vector2(1500, 42);

            var label = NewText("TelopText", transform, 42, Green, TextAnchor.UpperLeft);
            rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 70);
            rt.sizeDelta = new Vector2(1500, 140);

            Telop.SpeakerLabel = speaker;
            Telop.Label = label;
        }

        void BuildNarratorBand()
        {
            var band = NewPanel("NarratorBand", transform, new Color(0f, 0f, 0f, 0.88f));
            var rt = band.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(0, 170);

            _narrText = NewText("NarratorText", rt, 38, Color.white, TextAnchor.MiddleCenter);
            Fill(_narrText.rectTransform);
            _narrText.rectTransform.offsetMin = new Vector2(120, 10);
            _narrText.rectTransform.offsetMax = new Vector2(-120, -10);

            var skip = NewButton("SkipBtn", rt, "スキップ ▶", new Vector2(170, 48), () =>
            {
                var g = GameManager.I;
                if (g != null && g.Narrator != null) g.Narrator.RequestSkip();
            });
            var srt = (RectTransform)skip.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 1);
            srt.anchoredPosition = new Vector2(-20, -12);

            _narrBand = band.gameObject;
            _narrBand.SetActive(false);
        }

        void BuildButtons()
        {
            var holder = NewRect("Buttons", transform);
            holder.anchorMin = holder.anchorMax = new Vector2(1, 0);
            holder.pivot = new Vector2(1, 0);
            holder.anchoredPosition = new Vector2(-50, 60);
            holder.sizeDelta = new Vector2(210, 320);
            var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.LowerCenter;

            // push-to-talk button (press & hold)
            var talk = NewButton("TalkBtn", holder, "🎤 はなす(長押し)", new Vector2(210, 64), null);
            var trig = talk.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                var g = GameManager.I;
                if (g != null && g.Conversation != null) g.Conversation.OnMicPressed();
            });
            trig.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ =>
            {
                var g = GameManager.I;
                if (g != null && g.Conversation != null) g.Conversation.OnMicReleased();
            });
            trig.triggers.Add(up);
            // releasing outside the button must also stop recording
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                var g = GameManager.I;
                if (g != null && g.Mic != null && g.Mic.IsRecording && g.Conversation != null)
                    g.Conversation.OnMicReleased();
            });
            trig.triggers.Add(exit);
            _micHint = talk.GetComponentInChildren<Text>();
            _micHint.fontSize = 26;
            _micHint.text = "はなす(長押し)";

            NewButton("FeedBtn", holder, "エサをやる", new Vector2(210, 56), () =>
            {
                var g = GameManager.I;
                if (g != null && g.Feeding != null) g.Feeding.Feed();
                if (g != null && g.CRT != null) g.CRT.Tap();
            }).GetComponentInChildren<Text>().fontSize = 26;

            NewButton("GuideBtn", holder, "ガイド", new Vector2(210, 56), () =>
            {
                var g = GameManager.I;
                if (g != null && g.Narrator != null && !g.Narrator.IsPlaying)
                {
                    if (g.CRT != null) g.CRT.StaticBurst(0.5f, 0.3f);
                    g.Narrator.PlayGuide();
                }
            }).GetComponentInChildren<Text>().fontSize = 26;

            NewButton("SettingsBtn", holder, "設定", new Vector2(210, 56), ToggleSettings)
                .GetComponentInChildren<Text>().fontSize = 26;
        }

        void BuildSettingsPanel()
        {
            var panel = NewPanel("SettingsPanel", transform, PanelBg);
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(860, 640);

            var title = NewText("Title", rt, 44, Green, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -26);
            title.rectTransform.sizeDelta = new Vector2(0, 60);
            title.text = "― セッテイ ―";

            _mbtiButton = NewButton("MbtiBtn", rt, "", new Vector2(560, 58), CycleMbti);
            var mrt = (RectTransform)_mbtiButton.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1);
            mrt.pivot = new Vector2(0.5f, 1);
            mrt.anchoredPosition = new Vector2(0, -110);

            _statusReport = NewText("StatusReport", rt, 28, DimGreen, TextAnchor.UpperLeft);
            _statusReport.rectTransform.anchorMin = new Vector2(0, 1);
            _statusReport.rectTransform.anchorMax = new Vector2(1, 1);
            _statusReport.rectTransform.pivot = new Vector2(0.5f, 1);
            _statusReport.rectTransform.anchoredPosition = new Vector2(0, -200);
            _statusReport.rectTransform.sizeDelta = new Vector2(-140, 300);

            var close = NewButton("CloseBtn", rt, "とじる", new Vector2(220, 56), ToggleSettings);
            var crt2 = (RectTransform)close.transform;
            crt2.anchorMin = crt2.anchorMax = new Vector2(0.5f, 0);
            crt2.pivot = new Vector2(0.5f, 0);
            crt2.anchoredPosition = new Vector2(0, 28);

            _settingsPanel = panel.gameObject;
            _settingsPanel.SetActive(false);
        }

        // ---------- behaviour ----------

        void ToggleSettings()
        {
            bool show = !_settingsPanel.activeSelf;
            var g = GameManager.I;
            if (g != null && g.CRT != null) g.CRT.StaticBurst(0.55f, 0.3f);
            _settingsPanel.SetActive(show);
            if (show)
            {
                RefreshSettings();
                if (g != null && g.Narrator != null && !g.Narrator.IsPlaying)
                    StartCoroutine(g.Narrator.PlayLine("設定画面。あなたの性格タイプを登録すれば、彼はその真逆へと育っていく。", "settings_open"));
            }
        }

        void RefreshSettings()
        {
            var g = GameManager.I;
            if (g == null || g.Personality == null) return;
            string cur = g.Personality.Data.userMbti;
            _mbtiIndex = System.Array.IndexOf(MbtiOptions, string.IsNullOrEmpty(cur) ? "未設定" : cur);
            if (_mbtiIndex < 0) _mbtiIndex = 0;
            _mbtiButton.GetComponentInChildren<Text>().text = "あなたのMBTI: " + MbtiOptions[_mbtiIndex] + " (クリックで変更)";
            _statusReport.text = "【シーマン観察記録】\n" + g.Personality.StatusReport();
        }

        void CycleMbti()
        {
            var g = GameManager.I;
            if (g == null || g.Personality == null) return;
            _mbtiIndex = (_mbtiIndex + 1) % MbtiOptions.Length;
            g.Personality.SetUserMbti(_mbtiIndex == 0 ? "" : MbtiOptions[_mbtiIndex]);
            RefreshSettings();
        }

        public void OnStateChanged(GameState s)
        {
            bool rec = s == GameState.Listening;
            if (_recDot != null) _recDot.SetActive(rec);
            if (_recText != null) _recText.gameObject.SetActive(rec);
            if (_micHint != null)
            {
                switch (s)
                {
                    case GameState.Listening: _micHint.text = "きいている…(離して送信)"; break;
                    case GameState.Thinking: _micHint.text = "かんがえ中…"; break;
                    case GameState.Speaking: _micHint.text = "しゃべっている…"; break;
                    default: _micHint.text = "はなす(長押し)"; break;
                }
            }
        }

        public void UpdateServerStatus()
        {
            var g = GameManager.I;
            if (g == null || g.AI == null || _serverStatus == null) return;
            if (g.AI.ServerOnline && g.AI.Health != null)
            {
                _serverStatus.text = $"AI: ONLINE  [{g.AI.Health.llm_model}]  TTS: {g.AI.Health.tts_engine}  VRAM: {g.AI.Health.vram_gb:F1}GB";
                _serverStatus.color = DimGreen;
            }
            else
            {
                _serverStatus.text = "AI: OFFLINE (定型文モード) — SeemanServer/setup.ps1 を実行してください";
                _serverStatus.color = new Color(1f, 0.6f, 0.3f, 0.9f);
            }
        }

        void Update()
        {
            // live mic level while recording, so the user can SEE the mic working
            var g = GameManager.I;
            if (_recText != null && _recText.gameObject.activeSelf && g != null && g.Mic != null)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(g.Mic.CurrentLevel * 40f), 0, 6);
                _recText.text = "REC " + new string('▮', n) + new string('・', 6 - n);
            }
        }

        void UpdateMeters()
        {
            var g = GameManager.I;
            if (g == null || g.Personality == null || _meters == null) return;
            var d = g.Personality.Data;
            _meters.text = "ハラヘリ " + Bar(d.hunger) + "\nナツキ  " + Bar(d.affection);
            if (_osdClock != null) _osdClock.text = System.DateTime.Now.ToString("HH:mm");
            // blink REC
            if (_recDot != null && _recDot.activeSelf)
                _recDot.GetComponent<Image>().enabled = Mathf.PingPong(Time.unscaledTime * 2f, 1f) > 0.4f;
        }

        static string Bar(float v)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(v / 10f), 0, 10);
            return new string('▮', n) + new string('▯', 10 - n);
        }
    }
}
