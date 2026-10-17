namespace PersonalAccountingApi.Services;

public sealed class BookKeepingService : IMessageService
{
    // TODO: only claim messages that look like bookkeeping entries.
    public bool CanHandle(string text) => true;

    // TODO: parse the message and record the entry.
    public Task<string> HandleAsync(string userId, string text, CancellationToken ct = default)
        => Task.FromResult("Not implemented yet.");
}
