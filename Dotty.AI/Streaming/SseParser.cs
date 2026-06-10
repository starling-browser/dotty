namespace Dotty.AI.Streaming;

public static class SseParser
{
    public static async IAsyncEnumerable<string> ParseAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        string? dataBuffer = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var payload = line.AsSpan(6);
                if (payload.SequenceEqual("[DONE]"))
                    break;

                if (dataBuffer != null)
                {
                    // Multi-line data: append
                    dataBuffer += "\n" + payload.ToString();
                }
                else
                {
                    dataBuffer = payload.ToString();
                }
            }
            else if (line.Length == 0 && dataBuffer != null)
            {
                // Empty line signals end of event
                yield return dataBuffer;
                dataBuffer = null;
            }
        }

        // Yield any remaining buffered data
        if (dataBuffer != null)
            yield return dataBuffer;
    }
}
