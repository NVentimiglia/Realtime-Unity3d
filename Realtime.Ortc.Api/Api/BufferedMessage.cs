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
    ///     Class used for message buffering.
    /// </summary>
    public class BufferedMessage : IComparable
    {
        #region Constructor (1)

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="messagePart"></param>
        /// <param name="message"></param>
        public BufferedMessage(int messagePart, string message)
        {
            MessagePart = messagePart;
            Message = message;
        }

        #endregion

        /// <summary>
        ///     Compares the message parts to put them in ascending order.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            var result = 1;

            if (obj != null && obj.GetType() == typeof (BufferedMessage))
            {
                var objectToCompare = (BufferedMessage) obj;
                result = objectToCompare.MessagePart == MessagePart
                    ? 0
                    : objectToCompare.MessagePart > MessagePart ? -1 : 1;
            }

            return result;
        }

        #region Properties (2)

        public int MessagePart { get; set; }
        public string Message { get; set; }

        #endregion
    }
}