namespace Api.Polly.Api1.Intra
{
    public sealed class ExternalResponseHttp(HttpClient httpClient)
    {
        public async Task<HttpResponseMessage> HttpResponseMessageAsync()
        => await httpClient.GetAsync("https://localhost:7079/WeatherForecast");
    }
}
