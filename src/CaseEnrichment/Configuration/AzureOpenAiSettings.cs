using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Xrm.Sdk;

namespace MM.Dataverse.CaseEnrichment.Configuration
{
    [DataContract]
    public sealed class AzureOpenAiSettings
    {
        public const string DefaultApiVersion = "2024-10-21";

        [DataMember(Name = "endpoint")]
        public string Endpoint { get; set; }

        [DataMember(Name = "deployment")]
        public string Deployment { get; set; }

        [DataMember(Name = "apiKey")]
        public string ApiKey { get; set; }

        [DataMember(Name = "apiVersion")]
        public string ApiVersion { get; set; }

        public Uri ChatCompletionsUri
        {
            get
            {
                var baseUri = Endpoint.TrimEnd('/');
                return new Uri(string.Format(
                    "{0}/openai/deployments/{1}/chat/completions?api-version={2}",
                    baseUri,
                    Uri.EscapeDataString(Deployment),
                    Uri.EscapeDataString(ApiVersion)));
            }
        }

        /// <summary>
        /// Parses the step's secure configuration. Errors never echo the raw JSON back,
        /// because it contains the API key and exception text ends up in System Jobs.
        /// </summary>
        public static AzureOpenAiSettings Parse(string secureConfiguration)
        {
            if (string.IsNullOrWhiteSpace(secureConfiguration))
            {
                throw new InvalidPluginExecutionException(
                    "Case enrichment is not configured. Add the Azure OpenAI settings to the step's secure configuration.");
            }

            AzureOpenAiSettings settings;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(AzureOpenAiSettings));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(secureConfiguration)))
                {
                    settings = (AzureOpenAiSettings)serializer.ReadObject(stream);
                }
            }
            catch (SerializationException)
            {
                throw new InvalidPluginExecutionException(
                    "Case enrichment secure configuration is not valid JSON.");
            }

            settings.Validate();
            return settings;
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(Endpoint)
                || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidPluginExecutionException(
                    "Case enrichment secure configuration needs an https 'endpoint'.");
            }

            if (string.IsNullOrWhiteSpace(Deployment))
            {
                throw new InvalidPluginExecutionException(
                    "Case enrichment secure configuration needs a 'deployment' name.");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidPluginExecutionException(
                    "Case enrichment secure configuration needs an 'apiKey'.");
            }

            if (string.IsNullOrWhiteSpace(ApiVersion))
            {
                ApiVersion = DefaultApiVersion;
            }
        }
    }
}
