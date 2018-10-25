using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BFYOC.Functions
{
    public class CogServicesCredentials : ServiceClientCredentials
    {
        private readonly string _key;

        public CogServicesCredentials(string key)
        {
            _key = key;
        }

        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}