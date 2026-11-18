using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using MM.Dataverse.CaseEnrichment.Ai;
using MM.Dataverse.CaseEnrichment.Configuration;
using MM.Dataverse.CaseEnrichment.Tests.Fakes;
using Xunit;

namespace MM.Dataverse.CaseEnrichment.Tests
{
    public class AzureOpenAiClientTests
    {
        private const string CompletionBody =
            "{\"id\":\"chatcmpl-1\",\"choices\":[{\"index\":0,\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{\\\"category\\\":\\\"Billing\\\"}\"}}],\"usage\":{\"prompt_tokens\":120,\"completion_tokens\":40,\"total_tokens\":160}}";

        private readonly AzureOpenAiSettings _settings = AzureOpenAiSettings.Parse(TestData.SecureConfiguration);
        private readonly TraceLog _trace = new TraceLog();
        private readonly List<TimeSpan> _delays = new List<TimeSpan>();

        [Fact]
        public void Posts_a_json_mode_request_with_the_api_key_header()
        {
            var handler = new StubHttpHandler(Ok());

            var content = Client(handler).CompleteJson("system text", "user text");

            Assert.Equal("{\"category\":\"Billing\"}", content);
            var request = handler.Requests.Single();
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(_settings.ChatCompletionsUri, request.RequestUri);
            Assert.Equal("test-key-not-real", request.Headers.GetValues("api-key").Single());

            var body = handler.RequestBodies.Single();
            Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", body);
            Assert.Contains("\"role\":\"system\",\"content\":\"system text\"", body);
            Assert.Contains("\"role\":\"user\",\"content\":\"user text\"", body);
        }

        [Fact]
        public void Never_writes_the_api_key_to_the_trace()
        {
            var handler = new StubHttpHandler(Status((HttpStatusCode)429), Status(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}"));

            Assert.Throws<AiClientException>(() => Client(handler).CompleteJson("s", "u"));

            Assert.NotEmpty(_trace.Lines);
            Assert.DoesNotContain("test-key-not-real", _trace.All);
        }

        [Theory]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(503)]
        public void Retries_transient_failures(int status)
        {
            var handler = new StubHttpHandler(Status((HttpStatusCode)status), Ok());

            var content = Client(handler).CompleteJson("s", "u");

            Assert.NotNull(content);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Single(_delays);
        }

        [Fact]
        public void Honours_retry_after_but_caps_it()
        {
            var throttled = Status((HttpStatusCode)429);
            throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
            var throttledLong = Status((HttpStatusCode)429);
            throttledLong.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            var handler = new StubHttpHandler(throttled, throttledLong, Ok());

            Client(handler).CompleteJson("s", "u");

            Assert.Equal(new[] { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10) }, _delays);
        }

        [Fact]
        public void Gives_up_after_the_maximum_number_of_attempts()
        {
            var handler = new StubHttpHandler(
                Enumerable.Range(0, AzureOpenAiClient.MaxAttempts).Select(_ => Status((HttpStatusCode)429)).ToArray());

            var ex = Assert.Throws<AiClientException>(() => Client(handler).CompleteJson("s", "u"));

            Assert.Contains("429", ex.Message);
            Assert.Equal(AzureOpenAiClient.MaxAttempts, handler.Requests.Count);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(404)]
        public void Does_not_retry_client_errors(int status)
        {
            var handler = new StubHttpHandler(Status((HttpStatusCode)status, "{\"error\":{\"code\":\"DeploymentNotFound\"}}"));

            var ex = Assert.Throws<AiClientException>(() => Client(handler).CompleteJson("s", "u"));

            Assert.Contains(status.ToString(), ex.Message);
            Assert.Single(handler.Requests);
            Assert.Contains("DeploymentNotFound", _trace.All);
        }

        [Fact]
        public void Rejects_a_response_without_choices()
        {
            var handler = new StubHttpHandler(Ok("{\"choices\":[]}"));

            Assert.Throws<AiClientException>(() => Client(handler).CompleteJson("s", "u"));
        }

        [Fact]
        public void Traces_token_usage()
        {
            Client(new StubHttpHandler(Ok())).CompleteJson("s", "u");

            Assert.Contains("120 prompt tokens, 40 completion tokens", _trace.All);
        }

        private AzureOpenAiClient Client(StubHttpHandler handler)
        {
            return new AzureOpenAiClient(_settings, _trace, handler, _delays.Add);
        }

        private static HttpResponseMessage Ok(string body = CompletionBody)
        {
            return Status(HttpStatusCode.OK, body);
        }

        private static HttpResponseMessage Status(HttpStatusCode status, string body = "{}")
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
