// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     Static client for accessing presence data
    /// </summary>
    public class PresenceClient
    {
        private const string SubscriptionsPattern =
            "^{\"subscriptions\":(?<subscriptions>\\d*),\"metadata\":{(?<metadata>.*)}}$";

        private const string MetadataPattern = "\"([^\"]*|[^:,]*)*\":(\\d*)";
        private const string MetadataDetailPattern = "\"(.*)\":(\\d*)";

        /// <summary>
        ///     Gets the subscriptions in the specified channel and if active the first 100 unique metadata.
        /// </summary>
        /// <param name="url">Server containing the presence service.</param>
        /// <param name="isCluster">Specifies if url is cluster.</param>
        /// <param name="authenticationToken">Authentication token with access to presence service.</param>
        /// <param name="applicationKey">Application key with access to presence service.</param>
        /// <param name="channel">Channel with presence data active.</param>
        public static void GetPresence(string url, bool isCluster, string applicationKey, string authenticationToken,
            string channel, Action<OrtcException, Presence> callback)
        {
            BalancerClient.GetServerUrl(url, isCluster, applicationKey, (exception, s) =>
            {
                if (exception)
                    callback(exception, null);
                else
                    RealtimeProxy.Instance.StartCoroutine(GetPresenceAsync(s, applicationKey, authenticationToken,
                        channel, callback));
            });
        }


        /// <summary>
        ///     Enables presence for the specified channel with first 100 unique metadata if metadata is set to true.
        /// </summary>
        /// <param name="url">Server containing the presence service.</param>
        /// <param name="isCluster">Specifies if url is cluster.</param>
        /// <param name="applicationKey">Application key with access to presence service.</param>
        /// <param name="privateKey">The private key provided when the ORTC service is purchased.</param>
        /// <param name="channel">Channel to activate presence.</param>
        /// <param name="metadata">Defines if to collect first 100 unique metadata.</param>
        public static void EnablePresence(string url, bool isCluster, string applicationKey, string privateKey,
            string channel, bool metadata, Action<OrtcException, string> callback)
        {
            BalancerClient.GetServerUrl(url, isCluster, applicationKey, (exception, s) =>
            {
                if (exception)
                    callback(exception, null);
                else
                    RealtimeProxy.Instance.StartCoroutine(EnablePresenceAsync(s, applicationKey, privateKey, channel,
                        metadata, callback));
            });
        }

        /// <summary>
        ///     Disables presence for the specified channel.
        /// </summary>
        /// <param name="url">Server containing the presence service.</param>
        /// <param name="isCluster">Specifies if url is cluster.</param>
        /// <param name="applicationKey">Application key with access to presence service.</param>
        /// <param name="privateKey">The private key provided when the ORTC service is purchased.</param>
        /// <param name="channel">Channel to disable presence.</param>
        public static void DisablePresence(string url, bool isCluster, string applicationKey, string privateKey,
            string channel, Action<OrtcException, string> callback)
        {
            BalancerClient.GetServerUrl(url, isCluster, applicationKey, (exception, s) =>
            {
                if (exception)
                    callback(exception, null);
                else
                    RealtimeProxy.Instance.StartCoroutine(DiablePresenceAsync(s, applicationKey, privateKey, channel,
                        callback));
            });
        }


        private static IEnumerator GetPresenceAsync(string server, string applicationKey, string authenticationToken,
            string channel, Action<OrtcException, Presence> callback)
        {
            var presenceUrl = string.IsNullOrEmpty(server)
                ? server
                : server[server.Length - 1] == '/' ? server : server + "/";

            presenceUrl = string.Format("{0}presence/{1}/{2}/{3}", presenceUrl, applicationKey, authenticationToken,
                channel);

            var www = new WWW(presenceUrl);

            yield return www;

            if (string.IsNullOrEmpty(www.error))
            {
                callback(null, Deserialize(www.text));
            }
            else
            {
                Debug.LogError(www.error);
                callback(new OrtcException(OrtcExceptionReason.ConnectionError, "Failed to get presence from server"),  null);
            }
        }

        private static IEnumerator EnablePresenceAsync(string server, string applicationKey, string privateKey,
            string channel, bool metadata, Action<OrtcException, string> callback)
        {
            var presenceUrl = string.IsNullOrEmpty(server)
                ? server
                : server[server.Length - 1] == '/' ? server : server + "/";
            presenceUrl = string.Format("{0}presence/enable/{1}/{2}", presenceUrl, applicationKey, channel);

            var content = string.Format("privatekey={0}", privateKey);

            if (metadata)
            {
                content = string.Format("{0}&metadata=1", content);
            }

            var by = Encoding.UTF8.GetBytes(content);
            var www = new WWW(presenceUrl, by);

            yield return www;

            if (string.IsNullOrEmpty(www.error))
            {
                callback(null, www.text);
            }
            else
            {
                Debug.LogError(www.error);
                callback(new OrtcException(OrtcExceptionReason.ConnectionError, www.error), null);
            }
        }

        private static IEnumerator DiablePresenceAsync(string server, string applicationKey, string privateKey,
            string channel, Action<OrtcException, string> callback)
        {
            var presenceUrl = string.IsNullOrEmpty(server)
                ? server
                : server[server.Length - 1] == '/' ? server : server + "/";
            presenceUrl = string.Format("{0}presence/disable/{1}/{2}", presenceUrl, applicationKey, channel);

            var content = string.Format("privatekey={0}", privateKey);


            var by = Encoding.UTF8.GetBytes(content);
            var www = new WWW(presenceUrl, by);

            yield return www;

            Debug.Log(www.text);

            if (string.IsNullOrEmpty(www.error))
            {
                callback(null, www.text);
            }
            else
            {
                Debug.LogError(www.error);
                callback(new OrtcException(OrtcExceptionReason.ConnectionError, www.error), null);
            }
        }

        private static Presence Deserialize(string message)
        {
            var result = new Presence();

            if (string.IsNullOrEmpty(message))
                return result;

            var json = message.Replace("\\\\\"", @"""");
            json = Regex.Unescape(json);

            var presenceMatch = Regex.Match(json, SubscriptionsPattern, RegexOptions.None);

            int subscriptions;

            if (int.TryParse(presenceMatch.Groups["subscriptions"].Value, out subscriptions))
            {
                var metadataContent = presenceMatch.Groups["metadata"].Value;

                var metadataRegex = new Regex(MetadataPattern, RegexOptions.None);
                foreach (Match metadata in metadataRegex.Matches(metadataContent))
                {
                    if (metadata.Groups.Count <= 1)
                        continue;

                    var metadataDetailMatch = Regex.Match(metadata.Groups[0].Value, MetadataDetailPattern,
                        RegexOptions.None);

                    int metadataSubscriptions;

                    if (int.TryParse(metadataDetailMatch.Groups[2].Value, out metadataSubscriptions))
                    {
                        result.Metadata.Add(metadataDetailMatch.Groups[1].Value, metadataSubscriptions);
                    }
                }
            }

            result.Subscriptions = subscriptions;

            return result;
        }
    }
}