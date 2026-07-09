using System.Globalization;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Cnet.Sessions;

public static class SessionExtensions
{
    public static Session Session(this UpdateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var chatId =
            context.Update.Message?.Chat.Id
            ?? context.Update.EditedMessage?.Chat.Id
            ?? context.Update.CallbackQuery?.Message?.Chat.Id
            ?? context.FromId
            ?? throw new InvalidOperationException("The update has no chat or user to key a session on.");

        var userId = context.FromId ?? chatId;
        var key = string.Create(CultureInfo.InvariantCulture, $"cnet:session:{chatId}:{userId}");
        var storage = context.Services.GetRequiredService<ISessionStorage>();
        return new Session(storage, key, context.CancellationToken);
    }
}
