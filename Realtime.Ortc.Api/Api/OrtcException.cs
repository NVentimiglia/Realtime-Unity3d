// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     The exception returned by the ORTC cleint
    /// </summary>
    public enum OrtcExceptionReason
    {
        /// <summary>
        ///     Undefined error
        /// </summary>
        GenericError,

        /// <summary>
        ///     Not authorized
        /// </summary>
        Unauthorized,

        /// <summary>
        ///     Failed to receive a response
        /// </summary>
        Timeout,

        /// <summary>
        ///     Communication Error
        /// </summary>
        ConnectionError,

        /// <summary>
        ///     Server Error
        /// </summary>
        ServerError,

        /// <summary>
        ///     Bad Arguments
        /// </summary>
        InvalidArguments

        //
    }

    /// <summary>
    ///     An exception thrown by the Ortc service client.
    ///     Includes a Logic friendly reason code.
    /// </summary>
    public class OrtcException : Exception
    {
        /// <summary>
        ///     Ctor
        /// </summary>
        public OrtcException()
        {
        }

        /// <summary>
        ///     Ctor
        /// </summary>
        public OrtcException(OrtcExceptionReason reason)
        {
            Reason = reason;
        }

        /// <summary>
        ///     Ctor
        /// </summary>
        public OrtcException(OrtcExceptionReason reason, string message, Exception innerException)
            : base(message, innerException)
        {
            Reason = reason;
        }

        /// <summary>
        ///     Ctor
        /// </summary>
        public OrtcException(OrtcExceptionReason reason, Exception innerException)
            : base(innerException.Message, innerException)
        {
            Reason = reason;
        }

        /// <summary>
        ///     Ctor
        /// </summary>
        public OrtcException(OrtcExceptionReason reason, string message)
            : base(message)
        {
            Reason = reason;
        }

        /// <summary>
        ///     Ctor
        /// </summary>
        public OrtcException(string message)
            : base(message)
        {
        }

        /// <summary>
        ///     Logic friendly exception reason
        /// </summary>
        public OrtcExceptionReason Reason { get; set; }

        public static bool operator true(OrtcException x)
        {
            return x != null;
        }

        public static bool operator false(OrtcException x)
        {
            return x == null;
        }
    }
}