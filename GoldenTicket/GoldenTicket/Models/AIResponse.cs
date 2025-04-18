using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace GoldenTicket.Models;

public class AIResponse
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string MainTag { get; set; } = "";
    public string SubTags  { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public bool CallAgent { get; set; } = false;

    public static AIResponse Parse(string rawResponse)
    {
        var response = new AIResponse();

        // Regular expressions to match each field
        var titleMatch = Regex.Match(rawResponse, @"TITLE:\s*(.+)");
        var tagMatch = Regex.Match(rawResponse, @"PTAG:\s*(.+)");
        var subTagMatch = Regex.Match(rawResponse, @"PSUBTAG:\s*(.+)");
        var priorityMatch = Regex.Match(rawResponse, @"PRIORITY:\s*(.+)");
        var callAgentMatch = Regex.Match(rawResponse, @"SendToLiveAgent:\s*(true|false)", RegexOptions.IgnoreCase);
        var messageMatch = Regex.Match(rawResponse, @"Response:\s*(.+)", RegexOptions.Singleline); // Capture everything after "Response:"

        // Assign values if found
        if (titleMatch.Success) response.Title = titleMatch.Groups[1].Value.Trim();
        if (tagMatch.Success) response.MainTag = tagMatch.Groups[1].Value.Trim();
        if (subTagMatch.Success) response.SubTags = subTagMatch.Groups[1].Value.Trim();
        if (priorityMatch.Success) response.SubTags = subTagMatch.Groups[1].Value.Trim();
        if (callAgentMatch.Success) response.CallAgent = bool.Parse(callAgentMatch.Groups[1].Value.Trim());
        if (messageMatch.Success) response.Message = messageMatch.Groups[1].Value.Trim(); // This will now capture multi-line responses

        return response;
    }
    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
    public static AIResponse Unavailable() {
        return new AIResponse() {
            Title = "AI Unavailable, need live agent",
            Message = "Sorry, Chatbot service is currently down at the moment. Sending a live agent...",
            MainTag = "null",
            SubTags = "null",
            Priority = "Medium",
            CallAgent = true
        };
    }
}
