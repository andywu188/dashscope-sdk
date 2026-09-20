namespace Cnblogs.DashScope.Core.Internals;

/// <summary>
/// Process-wide shared <see cref="HttpClient"/> instances for downloading speech transcription results.
/// </summary>
internal static class SpeechTranscriptionDownloadClientCache
{
    private static readonly Dictionary<long, HttpClient> Clients = new();
    private static readonly object SyncRoot = new();

    public static HttpClient GetOrCreate(TimeSpan timeout)
    {
        var key = timeout.Ticks;
        lock (SyncRoot)
        {
            if (Clients.TryGetValue(key, out var client))
            {
                return client;
            }

            client = new HttpClient { Timeout = timeout };
            Clients.Add(key, client);
            return client;
        }
    }
}
