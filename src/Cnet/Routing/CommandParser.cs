namespace Cnet.Routing;

public sealed record ParsedCommand(string Name, string Arguments);

public static class CommandParser
{
    public static bool TryParse(string? text, out ParsedCommand command)
    {
        command = null!;
        if (string.IsNullOrWhiteSpace(text) || text[0] != '/')
        {
            return false;
        }

        var body = text[1..];
        var spaceIndex = body.IndexOf(' ', StringComparison.Ordinal);
        var name = spaceIndex < 0 ? body : body[..spaceIndex];
        var arguments = spaceIndex < 0 ? string.Empty : body[(spaceIndex + 1)..].Trim();

        var mentionIndex = name.IndexOf('@', StringComparison.Ordinal);
        if (mentionIndex >= 0)
        {
            name = name[..mentionIndex];
        }

        if (name.Length == 0)
        {
            return false;
        }

        command = new ParsedCommand(name.ToLowerInvariant(), arguments);
        return true;
    }
}
