namespace MM.Dataverse.CaseEnrichment.Ai
{
    public interface IAiClient
    {
        /// <summary>
        /// Sends a system/user prompt pair and returns the model's reply, which the
        /// caller expects to be a single JSON object.
        /// </summary>
        string CompleteJson(string systemPrompt, string userPrompt);
    }
}
