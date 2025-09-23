using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Api.Polly.Api1.Entities;
using Api.Polly.Api1.Intra;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using System.Text.Json;
using System.Threading;

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
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
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
            //var waitAndRetryForeverAsync = Policy.Handle<Exception>().WaitAndRetryForeverAsync(
            //                              sleepDurationProvider: time => delay,
            //                              onRetry: async (exception, retryCount, context) =>
            //                              {
            //                                  Console.WriteLine($"Retry Erro: {exception.Message}. Retry: {context}.");
            //                                  var data = new LogRetry(Guid.NewGuid().ToString(), DateTime.Now, retryCount, $"Retry Erro: {exception.Message}");
            //                                  // salva o log de retry na tabela DynamoDB
            //                                  await _dynamoDBContext.SaveAsync(data);

            //                                  //envia para fila SQS o log de retry
            //                                  await _amazonSQS.SendMessageAsync(new SendMessageRequest("sqs-logretry", JsonSerializer.Serialize(data)));

            //                                  // aqui simulo a cria��o de uma subscription no SNS, pois o SNS n�o envia email de verdade
            //                                  var request = new SubscribeRequest
            //                                  {
            //                                      TopicArn = "arn:aws:sns:ap-south-1:000000000000:sns-weatherforecast",
            //                                      Protocol = "email",
            //                                      Endpoint = $"Subscription criada! {data.Date.AddMinutes(retryCount)}"
            //                                  };

            //                                  await _snsClient.SubscribeAsync(request);
            //                              });

            var waitAndRetryForever = new ResiliencePipelineBuilder()
                                            .AddRetry(new RetryStrategyOptions
                                            {
                                                // Simula "forever" retry
                                                MaxRetryAttempts = int.MaxValue,
                                                // Tempo de espera entre tentativas
                                                DelayGenerator = args => new ValueTask<TimeSpan?>(delay),
                                                // Define quando tratar (nesse caso: exce��es)
                                                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                                                // Callback chamado a cada retry
                                                OnRetry = async args =>
                                                {
                                                    Console.WriteLine($"Retry Erro: {args.Outcome.Exception?.Message}. Retry: {args.AttemptNumber}.");
                                                    var data = new LogRetry(
                                                        Guid.NewGuid().ToString(),
                                                        DateTime.Now,
                                                        args.AttemptNumber,
                                                        $"Retry Erro: {args.Outcome.Exception?.Message}"
                                                    );
                                                    // salva no DynamoDB
                                                    await _dynamoDBContext.SaveAsync(data);
                                                    // envia para SQS
                                                    await _amazonSQS.SendMessageAsync(
                                                        new SendMessageRequest("sqs-logretry", JsonSerializer.Serialize(data))
                                                    );
                                                    // simula a cria��o de uma subscription no SNS
                                                    var request = new SubscribeRequest
                                                    {
                                                        TopicArn = "arn:aws:sns:ap-south-1:000000000000:sns-weatherforecast",
                                                        Protocol = "email",
                                                        Endpoint = $"Subscription criada! {data.Date.AddMinutes(args.AttemptNumber)}"
                                                    };

                                                    await _snsClient.SubscribeAsync(request);
                                                }
                                            })
                                            .Build();


            // Executa a requisi��o HTTP externa utilizando a pol�tica de retry definida.
            // A requisi��o � realizada atrav�s do m�todo HttpResponseMessageAsync da inst�ncia injetada ExternalResponseHttp.
            //var resut = await waitAndRetryForeverAsync.ExecuteAsync(_externalResponseHttp.HttpResponseMessageAsync);

            var result = await waitAndRetryForever.ExecuteAsync(async token => await _externalResponseHttp.HttpResponseMessageAsync(cancellationToken));

            // If the HTTP response indicates success, return the response content with HTTP 200 OK.
            if (result.IsSuccessStatusCode)
                return Ok(await result.Content.ReadFromJsonAsync<WeatherForecast[]>());

            // If the HTTP response indicates failure, return the status code from the external service and an error message.
            throw new Exception($"External service returned status code: {result.StatusCode}");
        }
        catch (Exception)
        {
            return BadRequest("Error calling external service");
        }
    }

    /// <summary>
    /// Aqui estou usando Polly.Core com ResiliencePipeline para criar uma pol�tica de Circuit Breaker.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [HttpGet("retry")]
    public async Task<IActionResult> Retry()
    {
        // Criar a pol�tica de Circuit Breaker
        var retryStrategies = new RetryStrategyOptions
        {
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(4),
            OnRetry = args =>
            {
                Console.WriteLine($"Retry {args.AttemptNumber} due to {args.Outcome.Exception?.Message}");
                return default;
            },
            ShouldHandle = new PredicateBuilder().Handle<Exception>()

        };

        var retryPipeline = new ResiliencePipelineBuilder()
                                        .AddRetry(retryStrategies)
                                        .Build();


        var result = await retryPipeline.ExecuteAsync(async token =>
        {
            var result = await _externalResponseHttp.HttpResponseMessageAsync(token);
            if (result.IsSuccessStatusCode)
                return await result.Content.ReadFromJsonAsync<WeatherForecast[]>();
            else
                throw new Exception(result.RequestMessage.ToString());

        });

        return Ok(result);
    }

    [HttpGet("timeout")]
    public async Task<IActionResult> Timeout()
    {
        var timeoutStrategy = new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(2),
            OnTimeout = static args =>
            {
                Console.WriteLine($"{args.Context.OperationKey} : Excution timed out after {args.Timeout.TotalSeconds} seconds");
                return default;
            }
        };

        var timeoutPipeline = new ResiliencePipelineBuilder()
                                        .AddTimeout(timeoutStrategy)
                                        .Build();

        var result = await timeoutPipeline.ExecuteAsync(async token =>
        {
            var result = await _externalResponseHttp.HttpResponseMessageAsync(token);
            if (result.IsSuccessStatusCode)
                return await result.Content.ReadFromJsonAsync<WeatherForecast[]>();
            else
                throw new Exception(result.RequestMessage.ToString());

        });
        return Ok(result);
    }

    [HttpGet("fallback")]
    public async Task<IActionResult> Fallback()
    {
        var fallbackStrategy = new FallbackStrategyOptions<List<WeatherForecast>>
        {
            ShouldHandle = new PredicateBuilder<List<WeatherForecast>>().Handle<Exception>().HandleResult(result => result is null),
            FallbackAction = args =>
            {
                var defaultWeather = new List<WeatherForecast>
                {
                    // retornar uma lista padr�o caso a chamada falhe
                    new() {
                        Date = DateOnly.FromDateTime(DateTime.UtcNow),
                        TemperatureC = 25,
                        Summary = "Sunny"
                    }
                };
                return Outcome.FromResultAsValueTask(defaultWeather);
            },
            OnFallback = args =>
            {
                Console.WriteLine("Fallback executed due to timeout.");
                return default;
            }
        };


        var fallbackPipeline = new ResiliencePipelineBuilder<List<WeatherForecast>>()
                                        .AddFallback(fallbackStrategy)
                                        .Build();

        var result = await fallbackPipeline.ExecuteAsync(async token =>
        {
            var response = await _externalResponseHttp.HttpResponseMessageAsync(token);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<WeatherForecast>>();
            else
                throw new Exception("Failed to fetch weather data");
        });
        return Ok(result);
    }

    [HttpGet("circuit-breaker")]
    public async Task<IActionResult> CircuitBreaker()
    {
        var circuitBreakerStrategy = new CircuitBreakerStrategyOptions
        {

            FailureRatio = 0.5, // 50% de falhas para abrir o circuito
            MinimumThroughput = 3, // n�mero m�nimo de chamadas antes de considerar a taxa de falhas
            SamplingDuration = TimeSpan.FromSeconds(30), // janela de tempo para calcular a taxa de falhas
            BreakDuration = TimeSpan.FromSeconds(15), // tempo que o circuito permanecer� aberto
            OnOpened = args =>
             {
                 Console.WriteLine("Circuit breaker opened!");
                 return ValueTask.CompletedTask;
             },
            OnClosed = args =>
            {
                Console.WriteLine("Circuit breaker closed!");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                Console.WriteLine("Circuit breaker half-opened, testing the waters...");
                return ValueTask.CompletedTask;
            },
            ShouldHandle = new PredicateBuilder().Handle<Exception>()
        };

        var retryStrategies = new RetryStrategyOptions
        {
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(4),
            OnRetry = args =>
            {
                Console.WriteLine($"Retry {args.AttemptNumber} due to {args.Outcome.Exception?.Message}");
                return default;
            },
            ShouldHandle = new PredicateBuilder().Handle<Exception>()

        };

        var circuitBreakerPipeline = new ResiliencePipelineBuilder()
                                        .AddRetry(retryStrategies)
                                        .AddCircuitBreaker(circuitBreakerStrategy)
                                        .Build();

        var result = await circuitBreakerPipeline.ExecuteAsync(async token =>
        {
            var result = await _externalResponseHttp.HttpResponseMessageAsync(token);
            if (result.IsSuccessStatusCode)
                return await result.Content.ReadFromJsonAsync<WeatherForecast[]>();
            else
                throw new Exception(result.RequestMessage.ToString());

        });
        return Ok(result);
    }

    /// <summary>
    /// Nesta caso estarei simulando que a API externa est� com problema e ir� disparar uma exce��o globa.
    /// quando temos uma exec��o global essa ser� tratada no Middleware de tratamento de exce��es.
    /// mais quando utilizamos um bloco try catch, a exce��o � tratada localmente e n�o chega ao Middleware.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [HttpGet("GetWeatherForecastExceptionGlobal")]
    public async Task<IActionResult> GetWeatherForecastExceptionGlobal(CancellationToken cancellationToken)
    {
        // Define uma pol�tica de retry do Polly que tentar� executar a opera��o at� 3 vezes caso ocorra alguma exce��o.
        // A cada tentativa de retry, registra no console o n�mero da tentativa e a mensagem da exce��o.
        //var retryPolicy = Policy.Handle<Exception>().RetryAsync(retryCount: 3, onRetry: (exception, retryCount) =>
        //{
        //    Console.WriteLine($"Retry {retryCount} due to {exception.Message}");
        //});

        var retryPolicy = new ResiliencePipelineBuilder<HttpResponseMessage>() // tipado pro retorno esperado
                                    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                                    {
                                        MaxRetryAttempts = 3,
                                        Delay = TimeSpan.Zero, // sem delay (equivalente ao RetryAsync antigo)
                                        ShouldHandle = new PredicateBuilder<HttpResponseMessage>().Handle<Exception>(), // trata exce��es
                                        OnRetry = args =>
                                        {
                                            Console.WriteLine($"Retry {args.AttemptNumber} due to {args.Outcome.Exception?.Message}");
                                            return default; // precisa retornar ValueTask
                                        }
                                    })
                                    .Build();

        // Executa a requisi��o HTTP externa utilizando a pol�tica de retry definida.
        // A requisi��o � realizada atrav�s do m�todo HttpResponseMessageAsync da inst�ncia injetada ExternalResponseHttp.
        //var resut = await retryPolicy.ExecuteAsync(_externalResponseHttp.HttpResponseMessageAsync);

        var result = await retryPolicy.ExecuteAsync(async token => await _externalResponseHttp.HttpResponseMessageAsync(cancellationToken));

        // If the HTTP response indicates success, return the response content with HTTP 200 OK.
        if (result.IsSuccessStatusCode)
            return Ok(await result.Content.ReadFromJsonAsync<WeatherForecast[]>());

        // If the HTTP response indicates failure, return the status code from the external service and an error message.
        throw new Exception($"External service returned status code: {result.StatusCode}");
    }
}
