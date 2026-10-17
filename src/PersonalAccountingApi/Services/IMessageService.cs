namespace PersonalAccountingApi.Services;

/// <summary>
/// A feature that answers user text messages (bookkeeping, reports, ...).
/// The webhook handler asks each registered service in registration order
/// and uses the first one whose <see cref="CanHandle"/> returns true.
/// </summary>
public interface IMessageService
{
    bool CanHandle(string text);

    /// <summary>Handles the message and returns the reply text.</summary>
    Task<string> HandleAsync(string userId, string text, CancellationToken ct = default);
}
