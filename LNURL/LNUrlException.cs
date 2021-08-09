using System;

namespace LNURL
{
    public class LNUrlException : Exception
    {
        public LNUrlException(string message) : base(message)
        {
        }
    }
}