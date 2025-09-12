using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Api.Polly.Api1.Intra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("WeatherForecast", client =>
{
    client.BaseAddress = new Uri("https://localhost:7079/");
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:4566", 
            AuthenticationRegion = "ap-south-1"
        };

        return new AmazonDynamoDBClient("test", "test", config);
    }); ;
}
else { }

builder.Services.AddScoped<IDynamoDBContext, DynamoDBContext>();

builder.Services.AddScoped<ExternalResponseHttp>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
