using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using NuGet.Versioning;

var targetArgument = new CliArgument<string?>("TARGETAPPFILE")
{
    Description = "The file path or URI for the C# file to run.",
    Arity = ArgumentArity.ZeroOrOne
};

var appArgsArgument = new CliArgument<string[]?>("APPARGS")
{
    Description = "The arguments to pass to the C# file.",
    Arity = ArgumentArity.ZeroOrMore
};

var rootCommand = new CliRootCommand("Runs standalone C# files.")
{
    targetArgument,
    appArgsArgument
};
rootCommand.SetAction(RunCommand);

VersionOptionAction.Apply(rootCommand);

var result = rootCommand.Parse(args);
var exitCode = await result.InvokeAsync();

return exitCode;

async Task<int> RunCommand(ParseResult parseResult, CancellationToken cancellationToken)
{
    var detectNewerVersionTask = Task.Run(() => DetectNewerVersion(cancellationToken), cancellationToken);

    var targetValue = parseResult.GetValue(targetArgument);

    string? targetFilePath = null;

    if (string.IsNullOrEmpty(targetValue))
    {
        // Read from stdin if no target file is specified
        var input = await Console.In.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrEmpty(input))
        {
            WriteError("No target file specified and no input from stdin.");
            return 1;
        }
        
        // Save input to a temporary file
        var tempFilePath = Path.GetTempFileName();
        await using (var fileStream = File.Create(tempFilePath))
        {
            using var writer = new StreamWriter(fileStream);
            await writer.WriteAsync(input.AsMemory(), cancellationToken);
        }

        // Change the file to a .cs extension
        var csFilePath = Path.Join(Path.GetDirectoryName(tempFilePath), Path.GetFileNameWithoutExtension(tempFilePath) + ".cs");
        File.Move(tempFilePath, csFilePath, true);

        targetFilePath = csFilePath;
    }
    else if ((targetValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || targetValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        && Uri.TryCreate(targetValue, UriKind.Absolute, out var uri))
    {
        // If it's a URI, download the file to a temporary location
        Write($"Downloading file from {uri}... ", ConsoleColor.DarkGray);
        using var client = new HttpClient();
        var response = await client.GetAsync(uri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Write("failed", ConsoleColor.DarkGray);
            WriteError($"Failed to download file from {uri}. Status code: {response.StatusCode}");
            return 1;
        }

        WriteLine("done", ConsoleColor.DarkGray);

        var tempFilePath = Path.GetTempFileName();
        await using (var fileStream = File.Create(tempFilePath))
        {
            await response.Content.CopyToAsync(fileStream, cancellationToken);
        }

        // Change the file to a .cs extension
        var csFilePath = Path.Join(Path.GetDirectoryName(tempFilePath), Path.GetFileNameWithoutExtension(tempFilePath) + ".cs");
        File.Move(tempFilePath, csFilePath, true);

        targetFilePath = csFilePath;
    }
    else if (File.Exists(targetValue))
    {
        // If it's a local file, use the provided path
        targetFilePath = Path.GetFullPath(targetValue);
    }
    else
    {
        WriteError($"File not found: {targetValue}");
        return 1;
    }

    var appArgs = parseResult.GetValue(appArgsArgument);

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
    private static readonly string[] RunArgs = ["run"];

    public async static Task<int> Run(string filePath, string[]? args, CancellationToken cancellationToken)
    {
        var arguments = RunArgs.Concat([filePath]);
        if (args is { Length: > 0 })
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

internal sealed class VersionOptionAction : SynchronousCliAction
{
    public static void Apply(CliRootCommand command)
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
