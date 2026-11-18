using System;
using Microsoft.Xrm.Sdk;
using MM.Dataverse.CaseEnrichment.Ai;
using MM.Dataverse.CaseEnrichment.Configuration;
using MM.Dataverse.CaseEnrichment.Enrichment;

namespace MM.Dataverse.CaseEnrichment
{
    /// <summary>
    /// Register on Create of incident, PostOperation, Asynchronous.
    /// Secure configuration: {"endpoint","deployment","apiKey","apiVersion"}.
    /// </summary>
    public sealed class CaseEnrichmentPlugin : IPlugin
    {
        // Allows cases created by another plugin or a flow (depth 2) but stops runaway
        // chains if someone also registers this step on Update.
        private const int MaxDepth = 2;

        // Dataverse caches plugin instances and calls Execute concurrently, so the only
        // fields here are immutable values handed over by the platform.
        private readonly string _secureConfiguration;
        private readonly Func<AzureOpenAiSettings, ITracingService, IAiClient> _aiClientFactory;

        public CaseEnrichmentPlugin(string unsecureConfiguration, string secureConfiguration)
            : this(secureConfiguration, (settings, tracing) => new AzureOpenAiClient(settings, tracing))
        {
        }

        internal CaseEnrichmentPlugin(
            string secureConfiguration,
            Func<AzureOpenAiSettings, ITracingService, IAiClient> aiClientFactory)
        {
            _secureConfiguration = secureConfiguration;
            _aiClientFactory = aiClientFactory;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (!IsCaseCreate(context, tracing))
            {
                return;
            }

            if (context.Depth > MaxDepth)
            {
                tracing.Trace("Depth {0} exceeds {1}; skipping enrichment.", context.Depth, MaxDepth);
                return;
            }

            if (!(context.InputParameters.TryGetValue("Target", out var target) && target is Entity incident))
            {
                throw new InvalidPluginExecutionException("Case enrichment expected the created case as Target.");
            }

            // Parsed per execution rather than in the constructor: an exception thrown from
            // the constructor surfaces as a generic load failure instead of a readable message.
            var settings = AzureOpenAiSettings.Parse(_secureConfiguration);

            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var organization = factory.CreateOrganizationService(context.UserId);

            var service = new CaseEnrichmentService(_aiClientFactory(settings, tracing), organization, tracing);
            service.Enrich(incident);
        }

        private static bool IsCaseCreate(IPluginExecutionContext context, ITracingService tracing)
        {
            if (context.MessageName == "Create" && context.PrimaryEntityName == CaseColumns.EntityName)
            {
                return true;
            }

            // A misregistered step should be obvious in the trace log, not silently do nothing.
            tracing.Trace(
                "Case enrichment is registered on {0} of {1}; it only handles Create of {2}.",
                context.MessageName,
                context.PrimaryEntityName,
                CaseColumns.EntityName);
            return false;
        }
    }
}
