using System.Collections.Concurrent;

namespace GameNight.Application.Timers;

/// <summary>
/// The real clock, owned by the runtime rather than by any game.
/// A game asks to be woken by returning an Effect.ScheduleTimer; this is what wakes it.
/// </summary>
public interface ISessionTimers : IDisposable
{
    void Schedule(string timerId, TimeSpan delay);

    void Cancel(string timerId);

    void CancelAll();
}

public sealed class SessionTimers : ISessionTimers
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();
    private readonly Func<string, ValueTask> _onElapsed;
    private readonly TimeProvider _time;
    private bool _disposed;

    public SessionTimers(Func<string, ValueTask> onElapsed, TimeProvider? time = null)
    {
        _onElapsed = onElapsed;
        _time = time ?? TimeProvider.System;
    }

    public void Schedule(string timerId, TimeSpan delay)
    {
        if (_disposed) return;

        Cancel(timerId);

        var cts = new CancellationTokenSource();
        _pending[timerId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, _time, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_pending.TryRemove(timerId, out var finished))
            {
                finished.Dispose();
            }

            // The session's own loop decides what this means. We only ring the bell.
            await _onElapsed(timerId).ConfigureAwait(false);
        });
    }

    public void Cancel(string timerId)
    {
        if (_pending.TryRemove(timerId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void CancelAll()
    {
        foreach (var key in _pending.Keys.ToList())
        {
            Cancel(key);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        CancelAll();
    }
}
