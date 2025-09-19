using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Api.Polly.Api1.Entities;
using Api.Polly.Api1.Intra;
using Microsoft.AspNetCore.Mvc;
using Polly;
using System.Text.Json;

namespace Api.Polly.Api1.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(ILogger<WeatherForecastController> logger,
                                        ExternalResponseHttp _externalResponseHttp,
                                        IDynamoDBContext _dynamoDBContext,
                                        IAmazonSQS _amazonSQS,
                                        IAmazonSimpleNotificationService _snsClient) : ControllerBase
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
                                              // salva o log de retry na tabela DynamoDB
                                              await _dynamoDBContext.SaveAsync(data);

                                              //envia para fila SQS o log de retry
                                              await _amazonSQS.SendMessageAsync(new SendMessageRequest("sqs-logretry", JsonSerializer.Serialize(data)));

                                              // aqui simulo a cria��o de uma subscription no SNS, pois o SNS n�o envia email de verdade
                                              var request = new SubscribeRequest
                                              {
                                                  TopicArn = "arn:aws:sns:ap-south-1:000000000000:sns-weatherforecast",
                                                  Protocol = "email",
                                                  Endpoint = $"Subscription criada! {data.Date.AddMinutes(retryCount)}"
                                              };

                                              await _snsClient.SubscribeAsync(request);
                                          });

            // Executa a requisi��o HTTP externa utilizando a pol�tica de retry definida.
            // A requisi��o � realizada atrav�s do m�todo HttpResponseMessageAsync da inst�ncia injetada ExternalResponseHttp.
            var resut = await waitAndRetryForeverAsync.ExecuteAsync(_externalResponseHttp.HttpResponseMessageAsync);

            // If the HTTP response indicates success, return the response content with HTTP 200 OK.
            if (resut.IsSuccessStatusCode)
                return Ok(await resut.Content.ReadFromJsonAsync<WeatherForecast[]>());

            // If the HTTP response indicates failure, return the status code from the external service and an error message.
            throw new Exception($"External service returned status code: {resut.StatusCode}");
        }
        catch (Exception)
        {
            return BadRequest("Error calling external service");
        }
    }

    /// <summary>
    /// Nesta caso estarei simulando que a API externa est� com problema e ir� disparar uma exce��o globa.
    /// quando temos uma exec��o global essa ser� tratada no Middleware de tratamento de exce��es.
    /// mais quando utilizamos um bloco try catch, a exce��o � tratada localmente e n�o chega ao Middleware.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [HttpGet("GetWeatherForecastExceptionGlobal")]
    public async Task<IActionResult> GetWeatherForecastExceptionGlobal()
    {
        // Define uma pol�tica de retry do Polly que tentar� executar a opera��o at� 3 vezes caso ocorra alguma exce��o.
        // A cada tentativa de retry, registra no console o n�mero da tentativa e a mensagem da exce��o.
        var retryPolicy = Policy.Handle<Exception>().RetryAsync(retryCount: 3, onRetry: (exception, retryCount) =>
        {
            Console.WriteLine($"Retry {retryCount} due to {exception.Message}");
        });

        // Executa a requisi��o HTTP externa utilizando a pol�tica de retry definida.
        // A requisi��o � realizada atrav�s do m�todo HttpResponseMessageAsync da inst�ncia injetada ExternalResponseHttp.
        var resut = await retryPolicy.ExecuteAsync(_externalResponseHttp.HttpResponseMessageAsync);

        // If the HTTP response indicates success, return the response content with HTTP 200 OK.
        if (resut.IsSuccessStatusCode)
            return Ok(await resut.Content.ReadFromJsonAsync<WeatherForecast[]>());

        // If the HTTP response indicates failure, return the status code from the external service and an error message.
        throw new Exception($"External service returned status code: {resut.StatusCode}");
    }
}
