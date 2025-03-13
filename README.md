# dotnet-cs

A tool that makes it easy to run standalone C# files.

> Currently requires [.NET 10 SDK daily build](https://github.com/dotnet/sdk/blob/main/documentation/package-table.md), version *10.0.100-preview.3.25163.13* or later

## Installation

```bash
dotnet tool install -g dotnet-cs
```

## Usage

```bash
cs <TARGETAPPFILE> [<APPARGS>]
```

### Arguments

Name  | Description
------|------------------------------------------------
&lt;TARGETAPPFILE&gt; | The path to the C# file to run. This can be a relative or absolute path, or a URI to a remote file.
&lt;APPARGS&gt; | Any arguments that should be passed to the C# app.

### Examples

Run a C# file named `hello.cs` in the current directory:

```bash
~/apps
$ cs hello.cs
Hello, world!
```

Run a C# file named `hello.cs` in a sub-directory and pass an argument to it:

```bash
~/apps
$ cs ./utils/hello.cs David
Hello, David!
```

Run a C# file from `https://github.com/DamianEdwards/csrun/tree/main/samples/hello.cs` and pass an argument to it:

```bash
~/apps
$ cs https://github.com/DamianEdwards/csrun/tree/main/samples/hello.cs Stephen
Hello, Stephen!
```
