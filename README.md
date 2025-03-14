# dotnet-cs

A tool that makes it easy to run C# from a file, URI, or stdin.

> Currently requires [.NET 10 SDK daily build](https://github.com/dotnet/sdk/blob/main/documentation/package-table.md), version *10.0.100-preview.3.25163.13* or later

## Installation

```shell
dotnet tool install -g dotnet-cs
```

## Usage

```shell
cs [<TARGETAPPFILE> [<APPARGS>...]]
```

```shell
<COMMAND> | cs [<APPARGS>...]
```

```shell
cs [<APPARGS>...] < <CSFILE>
```

### Arguments

Name  | Description
------|------------------------------------------------
`<TARGETAPPFILE>` | The file path or URI for the C# file to run.
`<APPARGS>` | Any arguments that should be passed to the C# app.

### Examples

Run a C# file named `hello.cs` in the current directory:

```shell
~/apps
$ cs hello.cs
Hello, world!
```

Run a C# file named `hello.cs` in a sub-directory and pass an argument to it:

```shell
~/apps
$ cs ./utils/hello.cs David
Hello, David!
```

Run a C# file from `https://github.com/DamianEdwards/csrun/tree/main/samples/hello.cs` and pass an argument to it:

```shell
~/apps
$ cs https://raw.githubusercontent.com/DamianEdwards/csrun/refs/heads/stdin/samples/hello.cs Stephen
Hello, Stephen!
```

Pipe C# code from a shell string literal to `cs`:

```shell
$ 'Console.WriteLine("Hello, world!");' | cs
Hello, world!
```

Pipe C# code from shell command output to `cs` via stdin:

```shell
$ cat hello.cs | cs
Hello, world!
```
