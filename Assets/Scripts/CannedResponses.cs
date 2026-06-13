using UnityEngine;

namespace Seeman
{
    /// <summary>Offline fallback lines used when the local AI server is unreachable.</summary>
    public static class CannedResponses
    {
        static readonly string[] Offline =
        {
            "…ふん、今日は頭が回らん。水が悪いんじゃないか。",
            "悪いが、考える気分じゃない。水槽の外のことは知らんよ。",
            "言葉ってのは水面の泡と同じだ。浮かんでは消える。今は消える方だ。",
            "サーバーが落ちてる? それはお前の世界の話だろう。こっちは水の中だ。",
            "…(口をパクパクさせている)…聞こえてはいるんだがな。",
            "今日は黙して語らず、だ。荘子も言っていた。言葉を忘れた者と語りたいとな。",
            "頭の中の魚が泳いでいない。つまり、考え事は休業中だ。",
        };

        static readonly string[] Greeting =
        {
            "…なんだ、お前か。まあ、いて悪いとは言わんがね。",
            "おう。今日も性懲りもなくガラスを叩きに来たのか。",
            "ふん、来たか。退屈してたところだ。礼は言わんぞ。",
            "…見られるってのは、存在を確認されるってことだ。悪い気はせんね。",
        };

        static readonly string[] Mutter =
        {
            "水槽の中から見れば、外のお前らこそ水槽の中だがね…。",
            "泳ぐから魚なのか、魚だから泳ぐのか…。",
            "ガラスってのは、こっちとそっちを分けてるようで、実は鏡なんだよ。",
            "腹が減るというのは、生きてる証拠らしい。面倒な証拠だ。",
            "今日も水は丸い。世界が丸いんだから当然か。",
            "深く考えるな、と言うやつほど何も考えていない。",
            "存在とは知覚されることだ、とバークリーは言った。つまり今、俺は存在してる。",
        };

        static readonly string[] Eating =
        {
            "…ふん、悪くない味だ。礼は言わんがね。",
            "餌で機嫌を取ろうって魂胆か。…まあ、乗ってやる。",
            "うまい。が、これで懐いたと思うなよ。",
            "食うために生きるのか、生きるために食うのか…まあ、食うがね。",
        };

        static readonly string[] CallOutsNamed =
        {
            "よう、{0}。…いや、用は無い。顔が見たかっただけだ。悪いか。",
            "おい、{0}。たまにはガラスを拭け。世界が曇って見える。",
            "{0}よ。お前、最近ちゃんと飯食ってるのか。…俺は食ってない。わかるな?",
            "…{0}。暇なら話し相手になれ。こっちは退屈で死にそうなんだ。",
            "{0}、ひとつ訊くがね。お前にとって俺は魚か、それとも友人か。…答えはいらん。",
        };

        static readonly string[] CallOutsAnon =
        {
            "よう。…そこにいるのは分かってるんだ。気配で。",
            "おい、そこの飼い主。たまにはこっちを見ろ。",
            "…暇だ。誰か、と言ってもお前しかいないがね。話でもするか。",
            "なあ。お前、名前はなんて言うんだ。いつまでも「おい」じゃ呼びにくい。",
            "よう。水の中から見るお前の顔は、今日も不景気だね。",
        };

        public static string PickCallOut(string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return CallOutsAnon[Random.Range(0, CallOutsAnon.Length)];
            return string.Format(CallOutsNamed[Random.Range(0, CallOutsNamed.Length)], userName);
        }

        public static string PickOffline() => Offline[Random.Range(0, Offline.Length)];
        public static string PickGreeting() => Greeting[Random.Range(0, Greeting.Length)];
        public static string PickMutter() => Mutter[Random.Range(0, Mutter.Length)];
        public static string PickEating() => Eating[Random.Range(0, Eating.Length)];
    }
}
