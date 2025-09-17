using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace Api.Polly.Api1.Extensions
{
    public static class AwsLocalStackExtensions
    {
        public static IServiceCollection AddLocalStackDynamoDb(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            if (environment.IsDevelopment())
            {
                var awsOptions = configuration.GetAWSOptions();
                awsOptions.Credentials = new BasicAWSCredentials("test", "test");
                awsOptions.DefaultClientConfig.AuthenticationRegion = "ap-south-1";
                awsOptions.DefaultClientConfig.ServiceURL = "http://localhost:4566";
                services.AddDefaultAWSOptions(awsOptions);

                services.AddAWSService<IAmazonDynamoDB>();
                services.AddAWSService<IAmazonSQS>();
                services.AddAWSService<IAmazonSimpleNotificationService>();
                
            }

            services.AddSingleton<IDynamoDBContext, DynamoDBContext>();

            return services;
        }
    }
}
