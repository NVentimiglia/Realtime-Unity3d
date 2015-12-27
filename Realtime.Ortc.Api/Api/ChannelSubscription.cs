// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     Details regarding a channel subscription
    /// </summary>
    public class ChannelSubscription
    {
        private bool _isSubscribed;
        private bool _isSubscribing;

        /// <summary>
        ///     Constructor for a new subscription
        /// </summary>
        /// <param name="subscribeOnReconnected"></param>
        /// <param name="onMessage"></param>
        public ChannelSubscription(bool subscribeOnReconnected, OnMessageDelegate onMessage)
        {
            SubscribeOnReconnected = subscribeOnReconnected;
            OnMessage = onMessage;
            IsSubscribed = false;
            IsSubscribing = false;
        }

        /// <summary>
        ///     Constructor for a new subscription
        /// </summary>
        public ChannelSubscription()
        {
        }

        /// <summary>
        ///     Is subscribing ?
        /// </summary>
        public bool IsSubscribing
        {
            get { return _isSubscribing; }
            set
            {
                if (value)
                {
                    _isSubscribed = false;
                }
                _isSubscribing = value;
            }
        }

        /// <summary>
        ///     Is Subscribed ?
        /// </summary>
        public bool IsSubscribed
        {
            get { return _isSubscribed; }
            set
            {
                if (value)
                {
                    _isSubscribing = false;
                }
                _isSubscribed = value;
            }
        }

        /// <summary>
        ///     Option to reconnect on disconnect
        /// </summary>
        public bool SubscribeOnReconnected { get; set; }

        /// <summary>
        ///     Channel Message Handler
        /// </summary>
        public OnMessageDelegate OnMessage { get; set; }
    }
}