using Api.Polly.Api1.Application;
using Api.Polly.Api1.Exceptions;
using Api.Polly.Api1.Extensions;
using Api.Polly.Api1.Intra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("WeatherForecast", client =>
{
    client.BaseAddress = new Uri("https://localhost:7079/");
});
builder.Services.AddScoped<ExternalResponseHttp>();

builder.Services.AddLocalStackDynamoDb(builder.Configuration, builder.Environment);
builder.Services.AddExceptionHandler<AppExcetpionHandler>();

builder.Services.AddScoped<LogRetryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseExceptionHandler(_ => { });
app.UseMiddleware<CustomExceptionMiddleware>();

app.MapControllers();

await app.RunAsync();
