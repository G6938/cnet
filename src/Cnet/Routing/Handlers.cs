namespace Cnet.Routing;

public interface ICommandHandler
{
    static abstract string Command { get; }

    Task HandleAsync(CommandContext context);
}

public interface ICallbackHandler
{
    static abstract string Prefix { get; }

    Task HandleAsync(CallbackContext context);
}

public interface IMessageHandler
{
    Task HandleAsync(MessageContext context);
}
