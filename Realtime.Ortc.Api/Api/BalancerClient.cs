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
    ///     A static class containing all the methods to communicate with the Ortc Balancer
    /// </summary>
    public class BalancerClient
    {
        private const string BalancerServerPattern = "^var SOCKET_SERVER = \"(?<server>http.*)\";$";

        /// <summary>
        ///     Retrieves an Ortc Server url from the Ortc Balancer
        /// </summary>
        /// <param name="url"></param>
        /// <param name="isCluster"></param>
        /// <param name="applicationKey"></param>
        /// <returns></returns>
        public static void GetServerUrl(string url, bool isCluster, string applicationKey,
            Action<OrtcException, string> callback)
        {
            if (!isCluster)
            {
                callback(null, url);
            }
            else
            {
                RealtimeProxy.Instance.StartCoroutine(GetServerFromBalancerAsync(url, isCluster, applicationKey,
                    callback));
            }
        }


        private static IEnumerator GetServerFromBalancerAsync(string url, bool isCluster, string applicationKey,
            Action<OrtcException, string> callback)
        {
            var parsedUrl = string.IsNullOrEmpty(applicationKey) ? url : url + "?appkey=" + applicationKey;

            var www = new WWW(parsedUrl);

            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Debug.LogError(www.error);
                callback(
                    new OrtcException(OrtcExceptionReason.ConnectionError, "Failed to get server url from balancer."),
                    string.Empty);
            }
            else
            {
                var match = Regex.Match(www.text, BalancerServerPattern);

                var re = match.Success ? match.Groups["server"].Value : string.Empty;

                callback(null, re);
            }
        }
    }
}