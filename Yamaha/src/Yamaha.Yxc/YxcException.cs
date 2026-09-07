using System;

namespace Yamaha.Yxc
{
    public sealed class YxcException : Exception
    {
        public YxcException(string message) : base(message)
        {
        }

        public YxcException(string message, Exception inner) : base(message, inner)
        {
        }

        public int? ResponseCode { get; init; }
    }
}
