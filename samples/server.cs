#!/usr/bin/env cs

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.OpenApi 10.0.0-*

var builder = WebApplication.CreateBuilder(args);

var settingsFile = $"{builder.Environment.ApplicationName}.settings.json";
var envSettingsFile = $"{builder.Environment.ApplicationName}.settings.{builder.Environment.EnvironmentName}json";
builder.Configuration.AddJsonFile(settingsFile, optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(envSettingsFile, optional: true, reloadOnChange: true);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/{name?}", (string? name) => new { Message = $"Hello, {name ?? "World"}!" })
    .WithName("SayHello");

app.Run();