using System.Collections;
using System.IO;
using UnityEngine;

namespace Seeman
{
    /// <summary>
    /// Keeps an eye on the local AI sidecar server. If it is offline and the bundled
    /// server exists in StreamingAssets, launches it automatically (first run will
    /// also trigger setup: venv + dependencies + Ollama model pull).
    /// </summary>
    public class SidecarLauncher : MonoBehaviour
    {
        public bool AutoLaunch = true;
        bool _launchTried;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(2f);
            var g = GameManager.I;
            if (g == null || g.AI == null) yield break;

            yield return g.AI.CheckHealth();
            if (!g.AI.ServerOnline && AutoLaunch && !_launchTried)
            {
                _launchTried = true;
                TryLaunch();
                // give the server time to come up, then re-check a few times
                for (int i = 0; i < 10 && !g.AI.ServerOnline; i++)
                {
                    yield return new WaitForSeconds(5f);
                    yield return g.AI.CheckHealth();
                }
            }
        }

        void TryLaunch()
        {
            string bat = Path.Combine(Application.streamingAssetsPath, "SeemanServer", "start_server.bat");
            if (!File.Exists(bat))
            {
                Debug.LogWarning("[Seeman] sidecar not found: " + bat);
                return;
            }
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = bat,
                    WorkingDirectory = Path.GetDirectoryName(bat),
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized
                };
                System.Diagnostics.Process.Start(psi);
                Debug.Log("[Seeman] sidecar launch requested.");
                var g = GameManager.I;
                if (g != null && g.UI != null)
                    g.UI.Telop.Show("システム", "ローカルAIサーバーを起動中… 初回はセットアップに時間がかかる。", 6f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Seeman] sidecar launch failed: " + e.Message);
            }
        }
    }
}
