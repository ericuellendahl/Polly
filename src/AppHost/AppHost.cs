var builder = DistributedApplication.CreateBuilder(args);

var httpclient = builder.AddExternalService("http-api2", new Uri("https://localhost:7079/"));

builder.AddProject<Projects.Api_Polly_Api1>("api-polly-api1")
       .WithReference(httpclient);


builder.AddProject<Projects.Api_Polly_Api2>("api-polly-api2");


await builder.Build().RunAsync();
