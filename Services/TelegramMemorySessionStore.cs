using System.Collections.Concurrent;
using CareerVault.Api.Models;

namespace CareerVault.Api.Services;

public sealed class TelegramMemorySessionStore
{
    private readonly ConcurrentDictionary<long, TelegramMemorySession> _sessions = new();

    public TelegramMemorySession Start(long chatId)
    {
        var session = new TelegramMemorySession(chatId);
        _sessions[chatId] = session;
        return session;
    }

    public bool TryGet(long chatId, out TelegramMemorySession session) =>
        _sessions.TryGetValue(chatId, out session!);

    public void Remove(long chatId) =>
        _sessions.TryRemove(chatId, out _);
}

public sealed class TelegramMemorySession(long chatId)
{
    private readonly object _sync = new();
    private readonly List<string> _contextLines = [];
    private readonly List<TelegramFileInfo> _files = [];
    private bool _waitingConfirmation;
    private IngestionTemporalContext? _referenceDate;

    public long ChatId { get; } = chatId;

    public void AddContext(string context, IngestionTemporalContext referenceDate)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return;
        }

        lock (_sync)
        {
            _contextLines.Add(context.Trim());
            _waitingConfirmation = false;
            _referenceDate = referenceDate;
        }
    }

    public void AddFiles(IEnumerable<TelegramFileInfo> files, IngestionTemporalContext referenceDate)
    {
        lock (_sync)
        {
            _files.AddRange(files);
            _waitingConfirmation = false;
            if (_files.Count > 0)
            {
                _referenceDate = referenceDate;
            }
        }
    }

    public void MarkWaitingConfirmation()
    {
        lock (_sync)
        {
            _waitingConfirmation = true;
        }
    }

    public TelegramMemorySessionSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new TelegramMemorySessionSnapshot(
                string.Join(Environment.NewLine, _contextLines),
                _files.ToList(),
                _waitingConfirmation,
                _referenceDate);
        }
    }
}

public sealed record TelegramMemorySessionSnapshot(
    string Context,
    IReadOnlyList<TelegramFileInfo> Files,
    bool WaitingConfirmation,
    IngestionTemporalContext? ReferenceDate)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Context) && Files.Count == 0;
}
