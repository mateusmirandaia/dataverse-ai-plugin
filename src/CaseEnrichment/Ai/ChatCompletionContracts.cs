using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MM.Dataverse.CaseEnrichment.Ai
{
    // Only the fields we send or read. DataContractJsonSerializer ignores the rest of
    // the response, which keeps us safe when the API adds properties.

    [DataContract]
    internal sealed class ChatCompletionRequest
    {
        [DataMember(Name = "messages", Order = 1)]
        public List<ChatMessage> Messages { get; set; }

        [DataMember(Name = "temperature", Order = 2)]
        public double Temperature { get; set; }

        [DataMember(Name = "max_tokens", Order = 3)]
        public int MaxTokens { get; set; }

        [DataMember(Name = "response_format", Order = 4)]
        public ResponseFormat ResponseFormat { get; set; }
    }

    [DataContract]
    internal sealed class ChatMessage
    {
        [DataMember(Name = "role", Order = 1)]
        public string Role { get; set; }

        [DataMember(Name = "content", Order = 2)]
        public string Content { get; set; }
    }

    [DataContract]
    internal sealed class ResponseFormat
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }
    }

    [DataContract]
    internal sealed class ChatCompletionResponse
    {
        [DataMember(Name = "choices")]
        public List<ChatChoice> Choices { get; set; }

        [DataMember(Name = "usage")]
        public ChatUsage Usage { get; set; }
    }

    [DataContract]
    internal sealed class ChatChoice
    {
        [DataMember(Name = "message")]
        public ChatMessage Message { get; set; }

        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    internal sealed class ChatUsage
    {
        [DataMember(Name = "prompt_tokens")]
        public int PromptTokens { get; set; }

        [DataMember(Name = "completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}
