using RadLine;
using Spectre.Console;

namespace DotNetCs;

internal static class LineEditorContextExtensions
{
    public static IDisposable ShowInlineMessage(this LineEditorContext context, Markup message)
    {
        // Make room for the message
        if (context.Buffer.CursorPosition + message.Length > Console.BufferWidth)
        {
            var offset = Console.BufferWidth - (Console.BufferWidth - message.Length);
            AnsiConsole.Cursor.Move(CursorDirection.Left, offset);
        }

        AnsiConsole.Cursor.Hide();
        AnsiConsole.Write(message);
        return new InlineMessage(message);
    }

    private class InlineMessage(Markup message) : IDisposable
    {
        public void Dispose()
        {
            AnsiConsole.Cursor.Move(CursorDirection.Left, message.Length);
            AnsiConsole.Console.Write(new string(' ', message.Length));
            AnsiConsole.Cursor.Show();
        }
    }
}
