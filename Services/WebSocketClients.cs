using System.Collections.Concurrent;
using System.Net.WebSockets;

public class WebSocketClients
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients
        = new ConcurrentDictionary<Guid, WebSocket>();

    public void Add(WebSocket webSocket)
    {
        _clients.GetOrAdd(Guid.NewGuid(), webSocket);
    }

    public IEnumerable<WebSocket> GetAll()
        => _clients.Values;

    public void RemoveDisconnectedClients()
        => _clients
            .Where(x => x.Value.State != WebSocketState.Open)
            .Select(_clients.TryRemove)
            .ToList();
}