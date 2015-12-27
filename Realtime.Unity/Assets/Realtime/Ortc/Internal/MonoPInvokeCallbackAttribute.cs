using System;

namespace Realtime.Ortc.Internal
{
    internal class MonoPInvokeCallbackAttribute : Attribute
    {
        // ReSharper disable once InconsistentNaming
        public Type type;

        public MonoPInvokeCallbackAttribute(Type t)
        {
            type = t;
        }
    }
}