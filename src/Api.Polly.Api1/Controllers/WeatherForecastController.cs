using Amazon.DynamoDBv2.DataModel;
using Api.Polly.Api1.Entities;
using Api.Polly.Api1.Intra;
using Microsoft.AspNetCore.Mvc;
using Polly;

namespace Api.Polly.Api1.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(ILogger<WeatherForecastController> logger,
                                        ExternalResponseHttp _externalResponseHttp,
                                        IDynamoDBContext _dynamoDBContext) : ControllerBase
{

    [HttpGet(Name = "GetWeatherForecast")]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Log an informational message indicating the start of the external service call.
            logger.LogInformation("Getting weather forecast from external service...");

            // Define uma pol�tica de retry do Polly que tentar� executar a opera��o at� 3 vezes caso ocorra alguma exce��o.
            // A cada tentativa de retry, registra no console o n�mero da tentativa e a mensagem da exce��o.
            //var retryPolicy = Policy.Handle<Exception>().RetryAsync(retryCount: 3, onRetry: (exception, retryCount) =>
            //{
            //    Console.WriteLine($"Retry {retryCount} due to {exception.Message}");
            //});


            // Define uma pol�tica de resili�ncia que repete indefinidamente 
            // a execu��o de uma opera��o sempre que ocorrer uma exce��o.
            //var retryForeverPolicy = Policy.Handle<Exception>().RetryForeverAsync(onRetry: (exception, retryCount, context) =>
            //{
            //    Console.WriteLine($"Retry {retryCount} due to {exception.Message}");
            //});


            TimeSpan delay = TimeSpan.FromSeconds(10);

            // Define uma pol�tica de retry do Polly que tenta executar a opera��o at� 3 vezes em caso de exce��o.
            // Aguarda 10 segundos entre cada tentativa e registra no console a mensagem de erro e o tempo de espera antes da pr�xima tentativa.
            //var waitAndRetryAsync = Policy.Handle<Exception>().WaitAndRetryAsync(
            //                                retryCount: 3,
            //                                sleepDurationProvider: retryAttempt => delay,
            //                                onRetry: (exception, retryCount) =>
            //                                {
            //                                    Console.WriteLine($"Retry Erro: {exception.Message}. Retry: {retryCount}.");
            //                                });

            // Define uma pol�tica de retry do Polly que tenta executar a opera��o indefinidamente em caso de exce��o.
            // Aguarda o tempo definido pela vari�vel 'delay' entre cada tentativa e registra no console a mensagem de erro e o n�mero da tentativa.
            var waitAndRetryForeverAsync = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                                          sleepDurationProvider: time => delay,
                                          onRetry: async (exception, retryCount, context) =>
                                          {
                                              Console.WriteLine($"Retry Erro: {exception.Message}. Retry: {context}.");
                                              var data = new LogRetry(Guid.NewGuid().ToString(), DateTime.Now, retryCount, $"Retry Erro: {exception.Message}");
                                              
                                              await _dynamoDBContext.SaveAsync(data);
                                          });

            // Executa a requisi��o HTTP externa utilizando a pol�tica de retry definida.
            // A requisi��o � realizada atrav�s do m�todo HttpResponseMessageAsync da inst�ncia injetada ExternalResponseHttp.
            var resut = await waitAndRetryForeverAsync.ExecuteAsync(_externalResponseHttp.HttpResponseMessageAsync);

            // If the HTTP response indicates success, return the response content with HTTP 200 OK.
            if (resut.IsSuccessStatusCode)
                return Ok(await resut.Content.ReadFromJsonAsync<WeatherForecast[]>());

            // If the HTTP response indicates failure, return the status code from the external service and an error message.
            return StatusCode((int)resut.StatusCode, "Error calling external service");
        }
        catch (Exception)
        {
            return BadRequest("Error calling external service");
        }
    }
}
