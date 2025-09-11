namespace Api.Polly.Api1.Intra;

public sealed class ExternalResponseHttp(IHttpClientFactory httpClientFactory)
{
    public async Task<HttpResponseMessage> HttpResponseMessageAsync()
    {
        var client = httpClientFactory.CreateClient("WeatherForecast");

        return await client.GetAsync("WeatherForecast");
    }
}
