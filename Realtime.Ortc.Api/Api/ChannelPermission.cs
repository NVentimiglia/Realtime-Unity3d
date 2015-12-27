namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     The channel permission.
    /// </summary>
    public enum ChannelPermissions
    {
        /// <summary>
        ///     Read permission
        /// </summary>
        Read = 'r',

        /// <summary>
        ///     Read and Write permission
        /// </summary>
        Write = 'w',

        /// <summary>
        ///     Presence permission
        /// </summary>
        Presence = 'p'
    }
}