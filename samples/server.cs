#!/usr/bin/env cs

#:sdk Microsoft.NET.Sdk.Web

var app = WebApplication.Create(args);

app.MapGet("/", () => new { Message = "Hello, World!" });

app.Run();
