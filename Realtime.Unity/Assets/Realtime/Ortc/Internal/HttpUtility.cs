// -------------------------------------
//  Domain		: IBT / Realtime.co
//  Author		: Nicholas Ventimiglia
//  Product		: Messaging and Storage
//  Published	: 2014
//  -------------------------------------

using System;
using System.Text;
using Realtime.Ortc.Api;

namespace Realtime.Ortc.Internal
{
    internal static class HttpUtility
    {
        internal static string UrlEncode(string str)
        {
            if (str == null)
                return null;

            return UrlEncode(str, Encoding.UTF8);
        }

        internal static string UrlEncode(string str, Encoding e)
        {
            if (str == null)
                return null;

#if UNITY_WSA
            var bytes = UrlEncodeToBytes(str, e);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
#else
            return Encoding.ASCII.GetString(UrlEncodeToBytes(str, e));
#endif
        }

        internal static byte[] UrlEncodeToBytes(string str, Encoding e)
        {
            if (str == null)
                return null;

            var bytes = e.GetBytes(str);
            return UrlEncode(bytes, 0, bytes.Length, false);
        }

        internal static byte[] UrlEncode(byte[] bytes, int offset, int count, bool alwaysCreateNewReturnValue)
        {
            var buffer = UrlEncode(bytes, offset, count);
            if (alwaysCreateNewReturnValue && buffer != null && buffer == bytes)
                return (byte[])buffer.Clone();

            return buffer;
        }

        internal static byte[] UrlEncode(byte[] bytes, int offset, int count)
        {
            if (!ValidateUrlEncodingParameters(bytes, offset, count))
                return null;

            var num = 0;
            var num2 = 0;
            for (var i = 0; i < count; i++)
            {
                var ch = (char)bytes[offset + i];
                if (ch == ' ')
                    num++;
                else if (!IsUrlSafeChar(ch))
                    num2++;
            }

            if (num == 0 && num2 == 0)
                return bytes;

            var buffer = new byte[count + num2 * 2];
            var num4 = 0;
            for (var j = 0; j < count; j++)
            {
                var num6 = bytes[offset + j];
                var ch2 = (char)num6;
                if (IsUrlSafeChar(ch2))
                {
                    buffer[num4++] = num6;
                }
                else if (ch2 == ' ')
                {
                    buffer[num4++] = 0x2b;
                }
                else
                {
                    buffer[num4++] = 0x25;
                    buffer[num4++] = (byte)IntToHex((num6 >> 4) & 15);
                    buffer[num4++] = (byte)IntToHex(num6 & 15);
                }
            }

            return buffer;
        }

        internal static bool IsUrlSafeChar(char ch)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                return true;

            switch (ch)
            {
                case '(':
                case ')':
                case '*':
                case '-':
                case '.':
                case '_':
                case '!':
                    return true;
            }

            return false;
        }

        internal static char IntToHex(int n)
        {
            if (n <= 9)
                return (char)(n + 0x30);

            return (char)(n - 10 + 0x61);
        }

        private static bool ValidateUrlEncodingParameters(byte[] bytes, int offset, int count)
        {
            if (bytes == null && count == 0)
                return false;

            if (bytes == null)
                throw new ArgumentNullException("bytes");

            if (offset < 0 || offset > bytes.Length)
                throw new ArgumentOutOfRangeException("offset");

            if (count < 0 || offset + count > bytes.Length)
                throw new ArgumentOutOfRangeException("count");

            return true;
        }

        internal static string GenerateConnectionEndpoint(string url, bool canHttps)
        {
            Uri uri;

            var connectionId = Strings.RandomString(8);
            var serverId = Strings.RandomNumber(1, 1000);

            uri = new Uri(url);

            var prefix = "https".Equals(uri.Scheme) && canHttps ? "wss" : "ws";

            var connectionUrl =
                new Uri(string.Format("{0}://{1}:{2}/broadcast/{3}/{4}/websocket", prefix, uri.DnsSafeHost, uri.Port,
                    serverId, connectionId)).AbsoluteUri;

            return connectionUrl;
        }
    }
}