using System.Text.Json.Serialization;

namespace Api.Polly.Api1.Entities;

public record class LogRetry
{
    public string Id { get; private set; }
    public DateTime Date { get; private set; }
    public int RetryCount { get; private set; }
    public string ErrorMessage { get; private set; }

    public LogRetry() { }

    [JsonConstructor]
    public LogRetry(string id, DateTime date, int retryCount, string errorMessage)
        => (Id, Date, RetryCount, ErrorMessage) = (id, date, retryCount, errorMessage);
}

