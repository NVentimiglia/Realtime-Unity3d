// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     Http Client for resolving the cluster url
    /// </summary>
    public class ClusterClient
    {
        #region static / const

        private const int MaxConnectionAttempts = 10;
        private const int RetryThreadSleep = 5;
        private const string ResponsePattern = "var SOCKET_SERVER = \"(?<host>.*)\";";

        #endregion

        #region public

        /// <summary>
        ///     Url for the cluster
        /// </summary>
        public string Url;

        /// <summary>
        ///     App key
        /// </summary>
        public string ApplicationKey;

        public ClusterClient()
        {
        }

        public ClusterClient(string url, string applicationKey)
        {
            Url = url;
            ApplicationKey = applicationKey;
        }

        /// <summary>
        ///     Gets the cluster server.
        /// </summary>
        /// <returns></returns>
        public static void GetClusterServer(string url, string applicationKey, Action<OrtcException, string> callback)
        {
            RealtimeProxy.Instance.StartCoroutine(GetAsync(url, applicationKey, callback));
        }

        /// <summary>
        ///     Does the get cluster server logic.
        /// </summary>
        /// <returns></returns>
        public static void GetClusterServerWithRetry(string url, string applicationKey,
            Action<OrtcException, string> callback)
        {
            RealtimeProxy.Instance.StartCoroutine(GetClusterServerWithRetryAsync(url, applicationKey, callback, 0));
        }

        #endregion

        #region private

        private static IEnumerator GetClusterServerWithRetryAsync(string url, string applicationKey,
            Action<OrtcException, string> callback, int retry)
        {
            if (retry > 0)
            {
                yield return new WaitForSeconds(RetryThreadSleep);
            }

            retry++;

            GetClusterServer(url, applicationKey, (exception, s) =>
            {
                if (exception == null && !string.IsNullOrEmpty(s))
                {
                    //success
                    callback(null, s);
                }
                else
                {
                    //max tries
                    if (retry > MaxConnectionAttempts)
                    {
                        callback(
                            new OrtcException(OrtcExceptionReason.ConnectionError,
                                "Unable to connect to the authentication server."), string.Empty);
                    }
                    else
                    {
                        //retry
                        RealtimeProxy.Instance.StartCoroutine(GetClusterServerWithRetryAsync(url, applicationKey,
                            callback, retry));
                    }
                }
            });
        }

        private static IEnumerator GetAsync(string url, string applicationKey, Action<OrtcException, string> callback)
        {
            var clusterRequestParameter = !string.IsNullOrEmpty(applicationKey)
                ? string.Format("appkey={0}", applicationKey)
                : string.Empty;

            var clusterUrl = string.Format("{0}{1}?{2}", url,
                !string.IsNullOrEmpty(url) && url[url.Length - 1] != '/' ? "/" : string.Empty, clusterRequestParameter);

            var hTask = new WWW(clusterUrl);

            yield return hTask;
            
            if (!string.IsNullOrEmpty(hTask.error))
            {
                Debug.LogError(hTask.error);
                callback(new OrtcException(OrtcExceptionReason.ConnectionError,"Unable to connect to the authentication server."), string.Empty);
            }
            else
            {
                callback(null, ParseResponse(hTask.text));
            }
        }

        /// <summary>
        ///     parses the response
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string ParseResponse(string input)
        {
            var match = Regex.Match(input, ResponsePattern);

            return match.Groups["host"].Value;
        }

        #endregion
    }
}