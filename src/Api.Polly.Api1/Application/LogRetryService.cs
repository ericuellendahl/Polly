using Amazon.SQS;
using Amazon.SQS.Model;
using Api.Polly.Api1.Entities;
using System.Text.Json;

namespace Api.Polly.Api1.Application
{
    public class LogRetryService(IAmazonSQS amazonSQS, ILogger<LogRetryService> logger)
    {

        public async Task<List<LogRetry>> GetMessagesAsync()
        {

            var listLogRetry = new List<LogRetry>();

            var queueUrl = "queue-logretry";
            var receiveMessageRequest = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 5
            };

            var receiveMessageResponse = await amazonSQS.ReceiveMessageAsync(receiveMessageRequest);
            if (receiveMessageResponse.Messages is null)
            {
                return listLogRetry;
            }

            foreach (var message in receiveMessageResponse.Messages)
            {
                logger.LogInformation("Message MessageId: {MessageId} and Message Body: {Body}", message.MessageId, message.Body);

                if (string.IsNullOrWhiteSpace(message.Body))
                    continue;

                var logRetry = JsonSerializer.Deserialize<LogRetry>(message.Body);

                listLogRetry.Add(logRetry!);

                //Após processar a mensagem, você pode deletá-la da fila
                var deleteMessageRequest = new DeleteMessageRequest
                {
                    QueueUrl = queueUrl,
                    ReceiptHandle = message.ReceiptHandle
                };

                await amazonSQS.DeleteMessageAsync(deleteMessageRequest);
            }

            return listLogRetry;
        }
    }
}

