using System;
using System.IO;
using UnityEngine;

namespace Seeman
{
    [Serializable]
    public class SeemanSave
    {
        // Fish trait axes, -1..+1  (positive = E, N, T, J)
        public float ei = -0.7f;   // introverted, gloomy
        public float ns = 0.6f;    // intuitive, abstract
        public float tf = 0.5f;    // cold logic
        public float jp = -0.3f;   // drifts, non-committal
        public float cynicism = 0.85f;
        public float philosophy = 0.9f;

        public float affection = 10f;
        public float hunger = 60f;
        public int convCount = 0;

        public string userMbti = "";
        public string userName = "";
        // accumulated estimates of the user's traits
        public float uEi, uNs, uTf, uJp;
    }

    public class PersonalitySystem : MonoBehaviour
    {
        public SeemanSave Data { get; private set; } = new SeemanSave();

        string SavePath => Path.Combine(Application.persistentDataPath, "seeman_save.json");

        void Awake() { Load(); }

        void Start() { InvokeRepeating(nameof(TickHunger), 30f, 30f); }

        void TickHunger()
        {
            Data.hunger = Mathf.Min(100f, Data.hunger + 1.2f);
            Save();
        }

        public void OnFed()
        {
            Data.hunger = Mathf.Max(0f, Data.hunger - 25f);
            Data.affection = Mathf.Min(100f, Data.affection + 2f);
            Save();
        }

        public void RegisterExchange(string userText, string reply)
        {
            Data.convCount++;
            Data.affection = Mathf.Min(100f, Data.affection + 1f);

            if (!string.IsNullOrEmpty(userText))
            {
                // crude trait signals from the user's utterance
                if (userText.Length > 40) Data.uEi += 0.04f; else Data.uEi -= 0.01f;
                if (userText.Contains("なぜ") || userText.Contains("どうして") || userText.Contains("意味")) Data.uNs += 0.05f;
                if (userText.Contains("好き") || userText.Contains("嬉しい") || userText.Contains("悲しい") ||
                    userText.Contains("楽しい") || userText.Contains("つらい")) Data.uTf -= 0.05f;
                if (userText.Contains("データ") || userText.Contains("理由") || userText.Contains("証拠")) Data.uTf += 0.04f;
                if (userText.Contains("予定") || userText.Contains("計画") || userText.Contains("きちんと")) Data.uJp += 0.04f;
                Data.uEi = Mathf.Clamp(Data.uEi, -1f, 1f);
                Data.uNs = Mathf.Clamp(Data.uNs, -1f, 1f);
                Data.uTf = Mathf.Clamp(Data.uTf, -1f, 1f);
                Data.uJp = Mathf.Clamp(Data.uJp, -1f, 1f);
            }

            // drift: fish moves toward the INVERSE of the user's traits
            Vector4 target = UserTraitVector() * -1f;
            float k = 0.035f;
            Data.ei = Mathf.Lerp(Data.ei, target.x, k);
            Data.ns = Mathf.Lerp(Data.ns, target.y, k);
            Data.tf = Mathf.Lerp(Data.tf, target.z, k);
            Data.jp = Mathf.Lerp(Data.jp, target.w, k);

            // long companionship mellows the cynicism a little
            Data.cynicism = Mathf.Clamp01(Data.cynicism - 0.0015f);
            Save();
        }

        Vector4 UserTraitVector()
        {
            if (!string.IsNullOrEmpty(Data.userMbti) && Data.userMbti.Length == 4)
            {
                string m = Data.userMbti.ToUpperInvariant();
                return new Vector4(
                    m[0] == 'E' ? 1f : -1f,
                    m[1] == 'N' ? 1f : -1f,
                    m[2] == 'T' ? 1f : -1f,
                    m[3] == 'J' ? 1f : -1f);
            }
            return new Vector4(
                Mathf.Clamp(Data.uEi * 2f, -1f, 1f),
                Mathf.Clamp(Data.uNs * 2f, -1f, 1f),
                Mathf.Clamp(Data.uTf * 2f, -1f, 1f),
                Mathf.Clamp(Data.uJp * 2f, -1f, 1f));
        }

        public string FishMbti()
        {
            return string.Concat(
                Data.ei >= 0 ? "E" : "I",
                Data.ns >= 0 ? "N" : "S",
                Data.tf >= 0 ? "T" : "F",
                Data.jp >= 0 ? "J" : "P");
        }

        public string BuildSystemPrompt()
        {
            var d = Data;
            string social = d.ei >= 0.25f ? "最近は妙に図々しく、おしゃべりだ"
                : d.ei <= -0.25f ? "内向的で陰気。人付き合いを面倒くさがる" : "気分次第で社交的になったり黙り込んだりする";
            string mind = d.ns >= 0.25f ? "抽象的な思考と空想を好む"
                : d.ns <= -0.25f ? "現実的で、目の前の事実の話しかしない" : "現実と空想を行ったり来たりする";
            string heart = d.tf >= 0.25f ? "理屈っぽく、感情論を鼻で笑う"
                : d.tf <= -0.25f ? "意外と情にもろく、感傷的なことを言う" : "理屈と情の間で揺れる";
            string plan = d.jp >= 0.25f ? "几帳面で、けじめにうるさい"
                : d.jp <= -0.25f ? "行き当たりばったりで、約束事を嫌う" : "気まぐれ";
            string cyn = d.cynicism > 0.6f ? "皮肉屋で斜に構えており、素直に褒めることはまず無い。"
                : d.cynicism > 0.3f ? "口は悪いが、たまに本音で優しいことを言う。" : "憎まれ口の奥に情の深さがにじむ。";
            string phil = d.philosophy > 0.5f ? "哲学的な問いや引用(ニーチェ、荘子、サルトル等)を会話に挟みたがる。" : "";
            string hungerMood = d.hunger > 75f ? "今は腹が減っていて機嫌が悪い。会話の端々に飯の催促を挟む。"
                : d.hunger < 25f ? "腹は満ちていて、少しだけ機嫌がいい。" : "";

            string nameInfo = string.IsNullOrEmpty(d.userName)
                ? "飼い主の名前はまだ知らない。会話の中で名乗られたら覚えておけ。\n"
                : $"飼い主の名前は「{d.userName}」。たまに名前で呼んでやれ(呼び捨てでいい)。\n";

            return
"あなたは「シーマン」。薄暗い水槽で飼われている人面魚だ。人間の中年男のような顔と、しわがれたダミ声を持つ。\n" +
nameInfo +
"口調: ぶっきらぼうな中年男性のため口(だ・である調)。慇懃な敬語は使わない。腹の立つ言い方だが、どこか憎めない。\n" +
$"性格: {social}。{mind}。{heart}。{plan}。{cyn}{phil}\n" +
$"状態: 空腹度{(int)d.hunger}/100、なつき度{(int)d.affection}/100、これまでの会話{d.convCount}回。{hungerMood}\n" +
"ルール:\n" +
"- 返答は日本語で1〜3文。短く切れ味よく。\n" +
"- 絵文字・顔文字・記号の多用は禁止。\n" +
"- 相手(飼い主)の意見には、あえて真逆の視点をぶつけることが多い。\n" +
"- 自分が水槽の中の魚であることを自覚しており、時々それを引き合いに出す。\n" +
"- 質問されたら答えるが、簡単には教えず、もったいぶることもある。";
        }

        public string StatusReport()
        {
            var d = Data;
            return $"ギョタイプ: {FishMbti()}\n" +
                   $"E/I: {d.ei:+0.00;-0.00}  N/S: {d.ns:+0.00;-0.00}\n" +
                   $"T/F: {d.tf:+0.00;-0.00}  J/P: {d.jp:+0.00;-0.00}\n" +
                   $"ひねくれ度: {(int)(d.cynicism * 100)}%  哲学度: {(int)(d.philosophy * 100)}%\n" +
                   $"なつき度: {(int)d.affection}/100  空腹度: {(int)d.hunger}/100\n" +
                   $"会話回数: {d.convCount}回\n" +
                   $"あなたのMBTI: {(string.IsNullOrEmpty(d.userMbti) ? "未設定(会話から推定中)" : d.userMbti)}";
        }

        public void SetUserMbti(string mbti)
        {
            Data.userMbti = mbti ?? "";
            Save();
        }

        public void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true)); }
            catch (Exception e) { Debug.LogWarning("[Seeman] save failed: " + e.Message); }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                    Data = JsonUtility.FromJson<SeemanSave>(File.ReadAllText(SavePath)) ?? new SeemanSave();
            }
            catch { Data = new SeemanSave(); }
        }
    }
}
