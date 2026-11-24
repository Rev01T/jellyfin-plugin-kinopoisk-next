using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    /// <summary>Recorded responses of the API from the <c>fixtures</c> folder.</summary>
    internal static class Fixtures
    {
        public static string Read(string name)
            => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
    }

    internal sealed class FakeResponse
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string Body { get; set; } = "{}";
        public TimeSpan? RetryAfter { get; set; }

        public static FakeResponse Ok(string body) => new FakeResponse { Body = body };
        public static FakeResponse Fixture(string name) => Ok(Fixtures.Read(name));
        public static FakeResponse Error(int status, string body = "{}") => new FakeResponse { Status = (HttpStatusCode)status, Body = body };
    }

    /// <summary>Answers requests with the responses chosen by a routing function and remembers the requests.</summary>
    internal sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Func<string, FakeResponse> _route;
        private readonly List<string> _requests = new List<string>();
        private readonly List<string> _rawRequests = new List<string>();

        public RoutingHandler(Func<string, FakeResponse> route)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
        }

        /// <summary>Path and query (unescaped) of every request received so far.</summary>
        public IReadOnlyList<string> Requests
        {
            get { lock (_requests) return _requests.ToArray(); }
        }

        /// <summary>Path and query of every request exactly as sent, with the escaping.</summary>
        public IReadOnlyList<string> RawRequests
        {
            get { lock (_requests) return _rawRequests.ToArray(); }
        }

        /// <summary>Number of requests to the given path (without the query).</summary>
        public int Count(string path)
        {
            var count = 0;
            foreach (var request in Requests)
                if (request == path || request.StartsWith(path + "?", StringComparison.Ordinal))
                    count++;
            return count;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var target = Uri.UnescapeDataString(request.RequestUri.PathAndQuery);
            lock (_requests)
            {
                _requests.Add(target);
                _rawRequests.Add(request.RequestUri.PathAndQuery);
            }

            var response = _route(target);
            var message = new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
            };
            if (response.RetryAfter.HasValue)
                message.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(response.RetryAfter.Value);
            return Task.FromResult(message);
        }
    }

    internal sealed class SingleHandlerClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public SingleHandlerClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false);
    }

    /// <summary>Collects log messages so that tests can check what was reported.</summary>
    internal sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new List<(LogLevel, string)>();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            lock (Entries)
                Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose()
            {
            }
        }
    }
}
