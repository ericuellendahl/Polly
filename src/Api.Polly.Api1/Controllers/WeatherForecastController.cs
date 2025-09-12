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

            // Define uma política de retry do Polly que tentará executar a operação até 3 vezes caso ocorra alguma exceção.
            // A cada tentativa de retry, registra no console o número da tentativa e a mensagem da exceção.
            //var retryPolicy = Policy.Handle<Exception>().RetryAsync(retryCount: 3, onRetry: (exception, retryCount) =>
            //{
            //    Console.WriteLine($"Retry {retryCount} due to {exception.Message}");
            //});


            // Define uma política de resiliência que repete indefinidamente 
            // a execução de uma operação sempre que ocorrer uma exceção.
            //var retryForeverPolicy = Policy.Handle<Exception>().RetryForeverAsync(onRetry: (exception, retryCount, context) =>
            //{
            //    Console.WriteLine($"Retry {retryCount} due to {exception.Message}");
            //});


            TimeSpan delay = TimeSpan.FromSeconds(10);

            // Define uma política de retry do Polly que tenta executar a operação até 3 vezes em caso de exceção.
            // Aguarda 10 segundos entre cada tentativa e registra no console a mensagem de erro e o tempo de espera antes da próxima tentativa.
            //var waitAndRetryAsync = Policy.Handle<Exception>().WaitAndRetryAsync(
            //                                retryCount: 3,
            //                                sleepDurationProvider: retryAttempt => delay,
            //                                onRetry: (exception, retryCount) =>
            //                                {
            //                                    Console.WriteLine($"Retry Erro: {exception.Message}. Retry: {retryCount}.");
            //                                });

            // Define uma política de retry do Polly que tenta executar a operação indefinidamente em caso de exceção.
            // Aguarda o tempo definido pela variável 'delay' entre cada tentativa e registra no console a mensagem de erro e o número da tentativa.
            var waitAndRetryForeverAsync = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
                                          sleepDurationProvider: time => delay,
                                          onRetry: async (exception, retryCount, context) =>
                                          {
                                              Console.WriteLine($"Retry Erro: {exception.Message}. Retry: {context}.");
                                              var data = new LogRetry(Guid.NewGuid().ToString(), DateTime.Now, retryCount, $"Retry Erro: {exception.Message}");
                                              
                                              await _dynamoDBContext.SaveAsync(data);
                                          });

            // Executa a requisição HTTP externa utilizando a política de retry definida.
            // A requisição é realizada através do método HttpResponseMessageAsync da instância injetada ExternalResponseHttp.
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
