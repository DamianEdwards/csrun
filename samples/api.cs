#!/usr/bin/env cs

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.OpenApi 10.0.0-*
#:package Scalar.AspNetCore 2.1.*

using Scalar.AspNetCore;

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
    app.MapScalarApiReference();

    app.MapGet("/", () => "Navigate to /scalar/v1 to see the API reference.")
    .ExcludeFromDescription();
}

app.MapGet("/hello", () => new HelloResponse("Hello, World!"))
    .WithName("HelloWorld")
    .WithDescription("Returns a hello world message.");

app.MapGet("/hello/{name}", (string name) => new HelloResponse($"Hello, {name}!"))
    .WithName("HelloName")
    .WithDescription("Returns a message customized for the supplied name.");

app.Run();

record struct HelloResponse(string Message);
