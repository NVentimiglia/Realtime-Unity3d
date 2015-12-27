using UnityEngine;

namespace Realtime.Ortc
{
    /// <summary>
    /// Configures the Api level Ortc factory with the unity client
    /// </summary>
    internal static class UnityOrtcStartup
    {
        static bool isConfigured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void ConfigureOrtc()
        {
            if (isConfigured)
                return;

            isConfigured = true;

            //Configure custom clients here.
            OrtcFactory.Init(() =>
            {
                return new UnityOrtcClient();
            });
        }
    }
}