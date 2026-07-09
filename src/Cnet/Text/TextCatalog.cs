using System.Globalization;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Cnet.Text;

public sealed class TextCatalog
{
    private readonly Dictionary<string, Dictionary<string, string>> _locales = new(StringComparer.OrdinalIgnoreCase);

    public string FallbackLocale { get; set; } = "en";

    public TextCatalog Add(string locale, string key, string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(template);

        if (!_locales.TryGetValue(locale, out var texts))
        {
            texts = new Dictionary<string, string>(StringComparer.Ordinal);
            _locales[locale] = texts;
        }

        texts[key] = template;
        return this;
    }

    public string Get(string? locale, string key, params object?[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var template = Lookup(locale, key) ?? Lookup(FallbackLocale, key) ?? key;
        if (args is not { Length: > 0 })
        {
            return template;
        }

        for (var i = 0; i < args.Length; i++)
        {
            template = template.Replace(
                "{" + i.ToString(CultureInfo.InvariantCulture) + "}",
                Convert.ToString(args[i], CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        return template;
    }

    private string? Lookup(string? locale, string key)
        => locale is not null
            && _locales.TryGetValue(locale, out var texts)
            && texts.TryGetValue(key, out var template)
            ? template
            : null;
}

public static class TextCatalogExtensions
{
    public static string T(this UpdateContext context, string key, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(context);
        var catalog = context.Services.GetService<TextCatalog>();
        return catalog is null ? key : catalog.Get(context.LanguageCode, key, args);
    }
}
