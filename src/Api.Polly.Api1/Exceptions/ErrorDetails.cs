using System.Text.Json;

namespace Api.Polly.Api1.Exceptions;

public class ErrorDetails
{
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }

    public override string ToString() => JsonSerializer.Serialize(this);
}
