using System;
using MM.Dataverse.CaseEnrichment.Ai;

namespace MM.Dataverse.CaseEnrichment.Tests.Fakes
{
    internal sealed class FakeAiClient : IAiClient
    {
        private readonly Func<string> _reply;

        public FakeAiClient(string reply)
            : this(() => reply)
        {
        }

        public FakeAiClient(Func<string> reply)
        {
            _reply = reply;
        }

        public int Calls { get; private set; }

        public string LastSystemPrompt { get; private set; }

        public string LastUserPrompt { get; private set; }

        public string CompleteJson(string systemPrompt, string userPrompt)
        {
            Calls++;
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            return _reply();
        }

        public static FakeAiClient Returning(string category, string sentiment, string priority, string summary)
        {
            return new FakeAiClient(string.Format(
                "{{\"category\":\"{0}\",\"sentiment\":\"{1}\",\"priority\":\"{2}\",\"summary\":\"{3}\"}}",
                category, sentiment, priority, summary));
        }
    }
}
