using System;
using UnityEngine;

namespace Realtime.Ortc
{
    /// <summary>
    /// Factory for creating clients
    /// </summary>
    /// <remarks>
    /// Initialized by platform specific factory 
    /// </remarks>
    public static class OrtcFactory
    {
        static Func<IOrtcClient> _create;
        static bool _isInit;

        /// <summary>
        /// creates a platform specific new ortc client
        /// </summary>
        /// <returns></returns>
        public static IOrtcClient Create()
        {
            if (!_isInit)
            {
                Debug.LogError("OrtcFactory not initialized. Please include a platform specific factory.");
                return null;
            }

            return _create();
        }
        
        /// <summary>
        /// Initialize from platform specific factory
        /// </summary>
        /// <param name="creation"></param>
        public static void Init(Func<IOrtcClient> creation)
        {
            _isInit = true;
            _create = creation;
        }
    }
}