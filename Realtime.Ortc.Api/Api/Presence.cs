using System.Collections.Generic;

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     Presence info, such as total subscriptions and metadata.
    /// </summary>
    public class Presence
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Presence" /> class.
        /// </summary>
        public Presence()
        {
            Subscriptions = 0;
            Metadata = new Dictionary<string, long>();
        }

        /// <summary>
        ///     Gets the subscriptions value.
        /// </summary>
        public long Subscriptions { get; set; }

        /// <summary>
        ///     Gets the first 100 unique metadata.
        /// </summary>
        public Dictionary<string, long> Metadata { get; set; }
    }
}