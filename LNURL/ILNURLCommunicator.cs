using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace LNURL;

public interface ILNURLCommunicator
{
    Task<JObject> SendRequest(Uri lnurl, CancellationToken cancellationToken = default);
}