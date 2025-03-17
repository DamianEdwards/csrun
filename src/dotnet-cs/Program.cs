using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using NuGet.Versioning;

var minimumSdkVersion = new SemanticVersion(10, 0, 100, "preview.3.25163.13");

var targetArgument = new Argument<string?>("TARGETAPPFILE")
{
    Description = "The file path or URI for the C# file to run.",
    Arity = ArgumentArity.ZeroOrOne
};

var appArgsArgument = new Argument<string[]?>("APPARGS")
{
    Description = "The arguments to pass to the C# file.",
    Arity = ArgumentArity.ZeroOrMore
};

var rootCommand = new RootCommand("Runs C# from a file, URI, or stdin.")
{
    targetArgument,
    appArgsArgument
};
rootCommand.SetAction(RunCommand);

VersionOptionAction.Apply(rootCommand);

var config = new CommandLineConfiguration(rootCommand)
{
    ProcessTerminationTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(30)
};
var result = rootCommand.Parse(args, config);
var exitCode = await result.InvokeAsync();

return exitCode;

async Task<int> RunCommand(ParseResult parseResult, CancellationToken cancellationToken)
{
    var detectNewerVersionTask = Task.Run(() => DetectNewerVersion(cancellationToken), cancellationToken);
    var validateSdkVersionTask = Task.Run(() => ValidateMinimumSdkVersion(minimumSdkVersion, cancellationToken), cancellationToken);

    var targetValue = parseResult.GetValue(targetArgument);
    var appArgsValue = parseResult.GetValue(appArgsArgument);

    string? targetFilePath = null;
    List<string> appArgs = [];

    // Add the args
    if (appArgsValue is not null && appArgsValue.Length > 0)
    {
        appArgs.AddRange(appArgsValue);
    }

    if (targetValue == "-")
    {
        // Interactive mode: read from stdin until Ctrl+R is pressed
        WriteLine("Reading from standard input. Press Ctrl+R to execute...", ConsoleColor.DarkGray);

        //var input = await ReadStdinUntilRun(cancellationToken);
        var input = await ReadStdinUntilCtrlR(cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }

        WriteLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            WriteLine();
            WriteError("No input provided.");
            return 1;
        }

        WriteLine("Running...", ConsoleColor.DarkGray);

        //WriteLine($"Code read: {input}");

        // Save input to a temporary file
        var tempFilePath = Path.GetTempFileName();
        await using (var fileStream = File.Create(tempFilePath))
        {
            using var writer = new StreamWriter(fileStream);
            await writer.WriteAsync(input.AsMemory(), default);
        }

        targetFilePath = ChangeFileExtension(tempFilePath, ".cs");
    }
    else if (Console.IsInputRedirected)
    {
        // Read from stdin if no target file is specified
        var input = await Console.In.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(input))
        {
            WriteError("No input provided.");
            return 1;
        }

        // Save input to a temporary file
        var tempFilePath = Path.GetTempFileName();
        await using (var fileStream = File.Create(tempFilePath))
        {
            using var writer = new StreamWriter(fileStream);
            await writer.WriteAsync(input.AsMemory(), cancellationToken);
        }

        targetFilePath = ChangeFileExtension(tempFilePath, ".cs");
    }
    else if ((targetValue?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true
              || targetValue?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true)
             && Uri.TryCreate(targetValue, UriKind.Absolute, out var uri))
    {
        // If it's a URI, download the file to a temporary location
        Write($"Downloading file from {uri}... ", ConsoleColor.DarkGray);
        using var client = new HttpClient();
        var response = await client.GetAsync(uri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            WriteLine("failed", ConsoleColor.DarkGray);
            WriteError($"Failed to download file from {uri}. Status code: {response.StatusCode}");
            return 1;
        }

        WriteLine("done", ConsoleColor.DarkGray);

        var tempFilePath = Path.GetTempFileName();
        await using (var fileStream = File.Create(tempFilePath))
        {
            await response.Content.CopyToAsync(fileStream, cancellationToken);
        }

        targetFilePath = ChangeFileExtension(tempFilePath, ".cs");
    }
    else if (File.Exists(targetValue))
    {
        // If it's a local file, use the provided path
        targetFilePath = Path.GetFullPath(targetValue);
    }
    else
    {
        if (string.IsNullOrEmpty(targetValue))
        {
            WriteError("No target file specified or no input provided.");
        }
        else
        {
            WriteError($"File not found: {targetValue}");
        }
        return 1;
    }

    var (hasMinRequiredSdkVersion, currentSdkVersion) = await validateSdkVersionTask;
    if (!hasMinRequiredSdkVersion)
    {
        WriteLine();
        WriteLine($"This tool requires .NET SDK version {minimumSdkVersion} or higher but current version is {currentSdkVersion}.", ConsoleColor.Red);
        return 1;
    }    

    // Run the target file
    var exitCode = await DotnetCli.Run(targetFilePath, appArgs, cancellationToken);

    // Process the detect newer version task
    try
    {
        var newerVersion = await detectNewerVersionTask;
        if (newerVersion is not null)
        {
            // TODO: Handle case when newer version is a pre-release version
            WriteLine();
            WriteLine($"A newer version ({newerVersion}) of dotnet-cs is available!", ConsoleColor.Yellow);
            WriteLine("Update by running 'dotnet tool update -g dotnet-cs'", ConsoleColor.Green);
        }
    }
    catch (Exception)
    {
        // Ignore exceptions from the detect newer version task
    }

    return exitCode;
}

static async Task<string> ReadStdinUntilCtrlR(CancellationToken cancellationToken)
{
    var sb = new StringBuilder();
    var buffer = new List<char>(1024);
    var cursorPosition = 0;

    Console.CursorVisible = true;

    while (!cancellationToken.IsCancellationRequested)
    {
        if (!Console.KeyAvailable)
        {
            await Task.Delay(50, cancellationToken);
            continue;
        }

        var keyInfo = Console.ReadKey(intercept: true);

        if (keyInfo.Key == ConsoleKey.R && keyInfo.Modifiers == ConsoleModifiers.Control)
        {
            sb.Append([.. buffer]);
            break;
        }

        // Handle arrow keys
        if (keyInfo.Key == ConsoleKey.LeftArrow && cursorPosition > 0)
        {
            cursorPosition--;
            Console.CursorLeft--;
        }
        else if (keyInfo.Key == ConsoleKey.RightArrow && cursorPosition < buffer.Count)
        {
            cursorPosition++;
            Console.CursorLeft++;
        }
        else if (keyInfo.Key == ConsoleKey.Home && cursorPosition > 0)
        {
            cursorPosition = 0;
            Console.CursorLeft = 0;
        }
        else if (keyInfo.Key == ConsoleKey.End && cursorPosition < buffer.Count)
        {
            cursorPosition = buffer.Count;
            Console.CursorLeft = buffer.Count;
        }
        else if (keyInfo.Key == ConsoleKey.Backspace && cursorPosition > 0)
        {
            cursorPosition--;
            buffer.RemoveAt(cursorPosition);

            // Redraw the current line
            var currentPos = Console.CursorLeft - 1;
            Console.CursorVisible = false;
            Console.CursorLeft = 0;
            Console.Write(new string(' ', buffer.Count + 1));
            Console.CursorLeft = 0;
            Console.Write(new string([.. buffer]));
            Console.CursorLeft = currentPos;
            Console.CursorVisible = true;
        }
        else if (!char.IsControl(keyInfo.KeyChar))
        {
            buffer.Insert(cursorPosition, keyInfo.KeyChar);
            cursorPosition++;

            // Redraw the current line
            var currentPos = Console.CursorLeft + 1;
            Console.CursorVisible = false;
            Console.CursorLeft = 0;
            Console.Write(new string([.. buffer]));
            Console.CursorLeft = currentPos;
            Console.CursorVisible = true;
        }
        else if (keyInfo.Key == ConsoleKey.Enter)
        {
            // Add current buffer to StringBuilder
            sb.AppendLine(new string([.. buffer]));
            buffer.Clear();
            cursorPosition = 0;
            Console.WriteLine();
        }
    }

    Console.CursorVisible = false;
    return sb.ToString();
}

static string ChangeFileExtension(string filePath, string newExtension)
{
    var newFilePath = Path.ChangeExtension(filePath, newExtension);
    File.Move(filePath, newFilePath, true);
    return newFilePath;
}

static async Task<(bool, SemanticVersion?)> ValidateMinimumSdkVersion(SemanticVersion minSdkVersionRequired, CancellationToken cancellationToken)
{
    var sdkVersion = await DotnetCli.Version(cancellationToken);
    if (sdkVersion is null)
    {
        return (false, null);
    }
    
    return sdkVersion >= minSdkVersionRequired ? (true, sdkVersion) : (false, sdkVersion);
}

static async Task<string?> DetectNewerVersion(CancellationToken cancellationToken)
{
    var currentVersionValue = VersionOptionAction.GetCurrentVersion();
    if (currentVersionValue is null || !SemanticVersion.TryParse(currentVersionValue, out var currentVersion))
    {
        return null;
    }

    var packageUrl = "https://api.nuget.org/v3-flatcontainer/dotnet-cs/index.json";
    using var httpClient = new HttpClient();
    var versions = await httpClient.GetFromJsonAsync(packageUrl, CsRunJsonContext.Default.NuGetVersions, cancellationToken: cancellationToken);

    if (versions?.Versions is null || versions.Versions.Length == 0)
    {
        return null;
    }

    var versionComparer = new VersionComparer();
    var latestVersion = currentVersion;
    foreach (var versionValue in versions.Versions)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            break;
        }

        if (SemanticVersion.TryParse(versionValue, out var version) && version > latestVersion)
        {
            latestVersion = version;
        }
    }

    return latestVersion > currentVersion ? latestVersion.ToString() : null;
}

static void WriteError(string message) => WriteLine(message, ConsoleColor.Red);

static void WriteLine(string? message = null, ConsoleColor? color = default)
{
    if (!string.IsNullOrEmpty(message))
    {
        Write(message, color);
    }
    Console.WriteLine();
}

static void Write(string? message = null, ConsoleColor? color = default)
{
    if (color is not null)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color.Value;
        Console.Write(message);
        Console.ForegroundColor = originalColor;
    }
    else
    {
        Console.Write(message);
    }
}

static class DotnetCli
{
    private static readonly string[] VersionArgs = ["--version"];
    private static readonly string[] RunArgs = ["run"];

    public static async Task<SemanticVersion?> Version(CancellationToken cancellationToken)
    {
        var startInfo = GetProcessStartInfo(VersionArgs);
        startInfo.RedirectStandardOutput = true;
        startInfo.Environment.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        startInfo.Environment.Add("DOTNET_NOLOGO", "1");
        startInfo.Environment.Add("DOTNET_GENERATE_ASPNET_CERTIFICATE", "0");
        startInfo.Environment.Add("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");

        using var process = Start(startInfo);

        var stdout = new StringBuilder();
        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.BeginOutputReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return null;
        }

        return SemanticVersion.TryParse(stdout.ToString().Trim(), out var value) ? value : null;
    }

    public async static Task<int> Run(string filePath, List<string>? args, CancellationToken cancellationToken)
    {
        var arguments = RunArgs.Concat([filePath]);
        if (args is { Count: > 0 })
        {
            arguments = arguments.Concat(args);
        }
        var startInfo = GetProcessStartInfo(arguments);
        startInfo.CreateNoWindow = false;
        startInfo.RedirectStandardOutput = false;
        var process = Start(startInfo);

        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }

    private static Process Start(IEnumerable<string> arguments) => Start(GetProcessStartInfo(arguments));

    private static Process Start(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };

        return process.Start() ? process : throw new Exception("Failed to start process");
    }

    private static ProcessStartInfo GetProcessStartInfo(IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            info.ArgumentList.Add(arg);
        }

        return info;
    }
}

[JsonSerializable(typeof(NuGetVersions))]
internal partial class CsRunJsonContext : JsonSerializerContext
{

}

internal class NuGetVersions
{
    [JsonPropertyName("versions")]
    public string[] Versions { get; set; } = [];
}

internal sealed class VersionOptionAction : SynchronousCommandLineAction
{
    public static void Apply(RootCommand command)
    {
        var versionOption = command.Options.FirstOrDefault(o => o.Name == "--version");
        if (versionOption is not null)
        {
            versionOption.Action = new VersionOptionAction();
        }
    }

    public override int Invoke(ParseResult parseResult)
    {
        var currentVersion = GetCurrentVersion();
        parseResult.Configuration.Output.WriteLine(currentVersion ?? "<unknown>");

        return 0;
    }

    public static string? GetCurrentVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informationalVersion))
        {
            // Remove the commit hash from the version string
            var versionParts = informationalVersion.Split('+');
            return versionParts[0];
        }

        return assembly.GetName().Version?.ToString();
    }
}
