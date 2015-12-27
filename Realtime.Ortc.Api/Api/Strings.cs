// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;
using System.Text;

namespace Realtime.Ortc.Api
{
    /// <summary>
    ///     Class used for operations with strings.
    /// </summary>
    public class Strings
    {
        /// <summary>
        ///     Randoms the number.
        /// </summary>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns></returns>
        public static int RandomNumber(int min, int max)
        {
            var random = new Random();
            return random.Next(min, max);
        }

        /// <summary>
        ///     Randoms the string.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        public static string RandomString(int size)
        {
            var builder = new StringBuilder();
            var random = new Random();
            char ch;

            for (var i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26*random.NextDouble() + 65)));
                builder.Append(ch);
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Generates an id.
        /// </summary>
        /// <returns></returns>
        public static string GenerateId(int size)
        {
            var g = Guid.NewGuid().ToString().Replace("-", "");

            return g.Substring(0, size);
        }
    }
}