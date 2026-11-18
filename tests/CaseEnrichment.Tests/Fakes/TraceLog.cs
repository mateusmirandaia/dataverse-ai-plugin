using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace MM.Dataverse.CaseEnrichment.Tests.Fakes
{
    internal sealed class TraceLog : ITracingService
    {
        public List<string> Lines { get; } = new List<string>();

        public string All => string.Join("\n", Lines);

        public void Trace(string format, params object[] args)
        {
            Lines.Add(args == null || args.Length == 0 ? format : string.Format(format, args));
        }
    }
}
