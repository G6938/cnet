using Cnet.Routing;

namespace Cnet.Pipeline;

public sealed record ErrorContext(Exception Exception, UpdateContext Update);

public sealed class ErrorHandlers
{
    public IList<Func<ErrorContext, Task>> Handlers { get; } = [];
}
