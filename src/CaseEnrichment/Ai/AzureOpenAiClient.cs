using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Microsoft.Xrm.Sdk;
using MM.Dataverse.CaseEnrichment.Configuration;

namespace MM.Dataverse.CaseEnrichment.Ai
{
    public sealed class AzureOpenAiClient : IAiClient
    {
        // The sandbox kills a plugin after 2 minutes. Three 30s attempts plus backoff
        // stays under that with room left for the Dataverse update.
        internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
        internal const int MaxAttempts = 3;
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

        private const int MaxResponseTokens = 400;

        private readonly AzureOpenAiSettings _settings;
        private readonly ITracingService _tracing;
        private readonly HttpMessageHandler _handler;
        private readonly Action<TimeSpan> _delay;

        public AzureOpenAiClient(AzureOpenAiSettings settings, ITracingService tracing)
            : this(settings, tracing, null, null)
        {
        }

        internal AzureOpenAiClient(
            AzureOpenAiSettings settings,
            ITracingService tracing,
            HttpMessageHandler handler,
            Action<TimeSpan> delay)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _tracing = tracing ?? throw new ArgumentNullException(nameof(tracing));
            _handler = handler;
            _delay = delay ?? Thread.Sleep;
        }

        public string CompleteJson(string systemPrompt, string userPrompt)
        {
            var body = Serialize(new ChatCompletionRequest
            {
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = userPrompt }
                },
                Temperature = 0.2,
                MaxTokens = MaxResponseTokens,
                ResponseFormat = new ResponseFormat { Type = "json_object" }
            });

            // A new client per execution: the sandbox worker can be recycled at any time,
            // so nothing is cached in static fields. Closing the connection avoids reusing
            // sockets the sandbox may already have torn down.
            using (var http = _handler == null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false))
            {
                http.Timeout = RequestTimeout;
                http.DefaultRequestHeaders.ConnectionClose = true;

                for (var attempt = 1; ; attempt++)
                {
                    HttpResponseMessage response;
                    try
                    {
                        response = Send(http, body);
                    }
                    catch (Exception ex) when (IsNetworkFailure(ex) && attempt < MaxAttempts)
                    {
                        _tracing.Trace("Azure OpenAI attempt {0} failed ({1}), retrying.", attempt, ex.GetType().Name);
                        _delay(Backoff(attempt));
                        continue;
                    }
                    catch (Exception ex) when (IsNetworkFailure(ex))
                    {
                        throw new AiClientException("Azure OpenAI could not be reached or did not respond in time.", ex);
                    }

                    using (response)
                    {
                        var status = (int)response.StatusCode;
                        _tracing.Trace("Azure OpenAI attempt {0} returned HTTP {1}.", attempt, status);

                        if (response.IsSuccessStatusCode)
                        {
                            return ReadContent(response);
                        }

                        if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                        {
                            _delay(RetryDelay(response, attempt));
                            continue;
                        }

                        // Error bodies from Azure OpenAI describe the problem (bad deployment name,
                        // content filter, quota) and never contain the key, so they are safe to trace.
                        _tracing.Trace("Azure OpenAI error body: {0}", Truncate(ReadBody(response), 1000));
                        throw new AiClientException(string.Format(
                            "Azure OpenAI returned HTTP {0} ({1}).", status, response.ReasonPhrase));
                    }
                }
            }
        }

        private HttpResponseMessage Send(HttpClient http, string body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _settings.ChatCompletionsUri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("api-key", _settings.ApiKey);

            // Plugins are synchronous; blocking here is the expected pattern in the sandbox.
            return http.SendAsync(request).GetAwaiter().GetResult();
        }

        private string ReadContent(HttpResponseMessage response)
        {
            ChatCompletionResponse completion;
            try
            {
                completion = Deserialize<ChatCompletionResponse>(ReadBody(response));
            }
            catch (SerializationException ex)
            {
                throw new AiClientException("Azure OpenAI returned a response that could not be read.", ex);
            }

            if (completion.Usage != null)
            {
                _tracing.Trace(
                    "Azure OpenAI usage: {0} prompt tokens, {1} completion tokens.",
                    completion.Usage.PromptTokens,
                    completion.Usage.CompletionTokens);
            }

            var choice = completion.Choices?.FirstOrDefault();
            if (choice?.Message?.Content == null)
            {
                throw new AiClientException("Azure OpenAI returned no content.");
            }

            if (choice.FinishReason == "length")
            {
                // JSON mode can still be cut off by max_tokens, which leaves invalid JSON.
                _tracing.Trace("Azure OpenAI stopped at the token limit; the reply may be truncated.");
            }

            return choice.Message.Content;
        }

        private static bool IsTransient(HttpStatusCode status)
        {
            var code = (int)status;
            return code == 429 || code >= 500;
        }

        private static bool IsNetworkFailure(Exception ex)
        {
            // HttpClient reports its own timeout as a cancelled operation on .NET Framework.
            return ex is OperationCanceledException || ex is HttpRequestException;
        }

        private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero)
            {
                return retryAfter.Value < MaxRetryDelay ? retryAfter.Value : MaxRetryDelay;
            }

            return Backoff(attempt);
        }

        private static TimeSpan Backoff(int attempt)
        {
            return TimeSpan.FromSeconds(Math.Pow(2, attempt));
        }

        private static string ReadBody(HttpResponseMessage response)
        {
            return response.Content == null
                ? string.Empty
                : response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }
    }
}
