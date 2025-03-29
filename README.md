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
cs -
```

```shell
cs [<APPARGS>...] < <CSFILE>
```

### Arguments

Name  | Description
------|------------------------------------------------
`<TARGETAPPFILE>` | The file path or URI for the C# file to run. Pass '-' to enter interactive terminal mode.
`<APPARGS>` | Any arguments that should be passed to the C# app.

### Options

Name  | Description
------|------------------------------------------------
`-?`, `-h`, `--help` | Show help information.
`--version` | Show version information.
`--edit` | Edit content in an interactive terminal C# editor


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

Enter interactive stdin mode to just type C# straight into the terminal:

```shell
$ cs -
Reading from standard input. Press Ctrl+R to execute..
Console.WriteLine("Hello, world!");
Running...
Hello, world!
```

Edit a C# file named `hello.cs` in the current directory:

```shell
~/apps
$ cs hello.cs --edit
Interactive C# editor! Press CTRL+R to run, CTRL+ALT+S to save, CTRL+Q to quit.
01 var name = args.Length > 0 ? args[0] : "World";
02 Console.WriteLine($"Hello, {name}!");
Hello, world!
```
