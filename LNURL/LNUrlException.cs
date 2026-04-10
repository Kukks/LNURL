using System;

namespace LNURL;

/// <summary>
/// Represents an error returned by an LNURL service endpoint or a protocol-level validation failure.
/// Thrown by LNURL operations when the service returns an error status or when response verification fails.
/// </summary>
public class LNUrlException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="LNUrlException"/> with the specified error message.
    /// </summary>
    /// <param name="message">The error message describing the LNURL failure.</param>
    public LNUrlException(string message) : base(message)
    {
    }
}
