using Realtime.Ortc.Api;
using UnityEngine;

namespace Realtime.Ortc
{
    /// <summary>
    /// Configures the Api level Ortc factory with the unity client
    /// </summary>
    internal static class UnityOrtcStartup
    {
        static bool isConfigured;

#if UNITY_5
        [RuntimeInitializeOnLoadMethod]
#endif
        internal static void ConfigureOrtc()
        {
            if (isConfigured)
                return;

            isConfigured = true;

            RealtimeProxy.ConfirmInit();

            //Configure custom clients here.
            OrtcFactory.Init(() =>
            {
                return new UnityOrtcClient();
            });
        }
    }
}