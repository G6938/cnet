using Telegram.Bot.Types.ReplyMarkups;

namespace Cnet.Keyboards;

public static class Keyboard
{
    public static InlineKeyboardBuilder Inline() => new();

    public static ReplyKeyboardBuilder Reply() => new();
}

public sealed class InlineKeyboardBuilder
{
    private readonly List<List<InlineKeyboardButton>> _rows = [];

    public InlineKeyboardBuilder Row()
    {
        _rows.Add([]);
        return this;
    }

    public InlineKeyboardBuilder Callback(string text, string callbackData)
    {
        CurrentRow().Add(InlineKeyboardButton.WithCallbackData(text, callbackData));
        return this;
    }

    public InlineKeyboardBuilder Url(string text, string url)
    {
        CurrentRow().Add(InlineKeyboardButton.WithUrl(text, url));
        return this;
    }

    public InlineKeyboardBuilder SwitchInlineQuery(string text, string query)
    {
        CurrentRow().Add(InlineKeyboardButton.WithSwitchInlineQuery(text, query));
        return this;
    }

    public InlineKeyboardMarkup Build() => new(_rows.Where(row => row.Count > 0));

    private List<InlineKeyboardButton> CurrentRow()
    {
        if (_rows.Count == 0)
        {
            _rows.Add([]);
        }

        return _rows[^1];
    }
}

public sealed class ReplyKeyboardBuilder
{
    private readonly List<List<KeyboardButton>> _rows = [];
    private bool _resize = true;
    private bool _oneTime;

    public ReplyKeyboardBuilder Row()
    {
        _rows.Add([]);
        return this;
    }

    public ReplyKeyboardBuilder Button(string text)
    {
        if (_rows.Count == 0)
        {
            _rows.Add([]);
        }

        _rows[^1].Add(new KeyboardButton(text));
        return this;
    }

    public ReplyKeyboardBuilder OneTime()
    {
        _oneTime = true;
        return this;
    }

    public ReplyKeyboardBuilder NoResize()
    {
        _resize = false;
        return this;
    }

    public ReplyKeyboardMarkup Build() => new(_rows.Where(row => row.Count > 0))
    {
        ResizeKeyboard = _resize,
        OneTimeKeyboard = _oneTime,
    };
}
