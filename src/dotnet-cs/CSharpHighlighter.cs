using RadLine;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DotNetCs;

internal class CSharpHighlighter : IHighlighter
{
    private readonly IHighlighter _highlighter;

    private static readonly string[] _keywords = [
        "var", "new", "using", "default", "async",
        "true", "false",
        "int", "long", "short", "double", "string",
        "class", "struct", "record", "interface", "enum",
        "readonly", "static", "public", "private", "protected", "internal", "partial",
    ];

    private static readonly string[] _controlFlow = [
        "if", "else", "switch", "case", "break", "return", "await", "yield",
        "for", "foreach", "do", "while"
    ];

    private static readonly string[] _symbols = [
        "{", "}", "(", ")", "<", ">", "!=", "==", "+", "-", "*", "%", "&", "|", "^", "&&", "||", "=>", "?"
    ];

    private static readonly string[] _numbers = ["0", "1", "2", "4", "5", "6", "7", "8", "9"];

    public CSharpHighlighter()
    {
        _highlighter = CreateHighlighter();
    }

    private static WordHighlighter CreateHighlighter()
    {
        var highlighter = new WordHighlighter();

        foreach (var keyword in _keywords)
        {
            highlighter.AddWord(keyword, new Style(foreground: Color.Blue));
        }

        foreach(var keyword in _controlFlow)
        {
            highlighter.AddWord(keyword, new Style(foreground: Color.MediumPurple1));
        }

        foreach (var symbol in _symbols)
        {
            highlighter.AddWord(symbol, new Style(foreground: Color.MediumPurple));
        }

        foreach (var number in _numbers)
        {
            highlighter.AddWord(number, new Style(foreground: Color.Magenta1));
        }

        highlighter
            // Types
            .AddWord("Console", new Style(foreground: Color.Aquamarine1))
            // Strings
            .AddWord("\"", new Style(foreground: Color.Orange1))
            // Comments
            .AddWord("/", new Style(foreground: Color.Green))
            ;

        return highlighter;
    }
    public IRenderable BuildHighlightedText(string text)
    {
        return _highlighter.BuildHighlightedText(text);
    }
}
