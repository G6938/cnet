using Cnet.Albums;
using Cnet.Sessions;
using Telegram.Bot.Types.Enums;

namespace Cnet.Routing;

public sealed class CnetRouter
{
    private readonly Dictionary<string, Func<CommandContext, Task>> _commands = new(StringComparer.Ordinal);
    private readonly List<(string Prefix, Func<CallbackContext, Task> Handler)> _callbacks = [];
    private readonly List<Func<MessageContext, Task>> _messageHandlers = [];
    private readonly Dictionary<string, Func<MessageContext, Task>> _stateHandlers = new(StringComparer.Ordinal);
    private readonly List<Func<AlbumContext, Task>> _albumHandlers = [];
    private readonly List<(UpdateType Type, Func<UpdateContext, Task> Handler)> _updateHandlers = [];

    public void AddCommand(string command, Func<CommandContext, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(handler);
        _commands[command.TrimStart('/').ToLowerInvariant()] = handler;
    }

    public void AddCallback(string prefix, Func<CallbackContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(handler);
        _callbacks.Add((prefix, handler));
        _callbacks.Sort((left, right) => right.Prefix.Length.CompareTo(left.Prefix.Length));
    }

    public void AddMessageHandler(Func<MessageContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _messageHandlers.Add(handler);
    }

    public void AddStateHandler(string state, Func<MessageContext, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentNullException.ThrowIfNull(handler);
        _stateHandlers[state] = handler;
    }

    public void AddAlbumHandler(Func<AlbumContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _albumHandlers.Add(handler);
    }

    public void AddUpdateHandler(UpdateType type, Func<UpdateContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _updateHandlers.Add((type, handler));
    }

    public async Task RouteAsync(UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var update = context.Update;

        if (update.CallbackQuery is { } callbackQuery)
        {
            var data = callbackQuery.Data ?? string.Empty;
            foreach (var (prefix, handler) in _callbacks)
            {
                if (data.StartsWith(prefix, StringComparison.Ordinal))
                {
                    await handler(new CallbackContext(context, callbackQuery, data[prefix.Length..])).ConfigureAwait(false);
                    return;
                }
            }

            await context.Client.AnswerCallbackAsync(callbackQuery.Id, cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);
        }

        if (update.Message is { From: not null } message)
        {
            if (CommandParser.TryParse(message.Text, out var command)
                && _commands.TryGetValue(command.Name, out var commandHandler))
            {
                await commandHandler(new CommandContext(context, message, command.Name, command.Arguments)).ConfigureAwait(false);
                return;
            }

            if (message.Text is null || !message.Text.StartsWith('/'))
            {
                if (_stateHandlers.Count > 0)
                {
                    var state = await context.Session().GetStateAsync().ConfigureAwait(false);
                    if (state is not null && _stateHandlers.TryGetValue(state, out var stateHandler))
                    {
                        await stateHandler(new MessageContext(context, message)).ConfigureAwait(false);
                        return;
                    }
                }

                foreach (var handler in _messageHandlers)
                {
                    await handler(new MessageContext(context, message)).ConfigureAwait(false);
                }

                if (_messageHandlers.Count > 0)
                {
                    return;
                }
            }
        }

        foreach (var (type, handler) in _updateHandlers)
        {
            if (update.Type == type)
            {
                await handler(context).ConfigureAwait(false);
            }
        }
    }

    public async Task RouteAlbumAsync(AlbumContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var handler in _albumHandlers)
        {
            await handler(context).ConfigureAwait(false);
        }
    }
}
