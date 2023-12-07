using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace simple_chat.Controllers;

public record Message(string name, string? text);

[ApiController]
[Route("[controller]")]
public class WebSocketController : ControllerBase
{
    private readonly WebSocketClients _clients;
    private readonly GlobalChatHistoryService _historyService;
    private readonly ILogger<WebSocketController> _logger;

    public WebSocketController(WebSocketClients clients, ILogger<WebSocketController> logger, GlobalChatHistoryService historyService)
    {
        _clients = clients;
        _logger = logger;
        _historyService = historyService;
    }

    [HttpGet]
    public async Task AcceptWebSocketAsync(CancellationToken cancellationToken = default)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            throw new InvalidOperationException("Only WebSocket connection are allowed.");
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _clients.Add(webSocket);

        await EchoAsync(webSocket, cancellationToken);
    }

    private async Task SendToAllAsync(Message message, CancellationToken cancellationToken = default)
    {
        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);

        var sendTasks = _clients
            .GetAll()
            .Select(x => x.State != WebSocketState.Open ? Task.CompletedTask : x.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken));

        await Task.WhenAll(sendTasks);
    }

    private async Task SendChatHistoryAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        foreach (var m in _historyService.GetLastMessages())
        {
            var messageJson = JsonSerializer.Serialize(m);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);

            await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, cancellationToken);
        }
    }

    private async Task EchoAsync(WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        await SendChatHistoryAsync(webSocket, cancellationToken);

        var buffer = new byte[4096];
        var receiveBuffer = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), cancellationToken);

        while (!receiveBuffer.CloseStatus.HasValue)
        {
            if (receiveBuffer.MessageType != WebSocketMessageType.Text)
            {
                break;
            }

            var messageJson = Encoding.UTF8.GetString(buffer).TrimEnd('\x00'); // NOTE: some black magic to trim NULL terminated chars 
            _logger.LogInformation("Received message {message}", messageJson);

            var message = JsonSerializer.Deserialize<Message>(messageJson);
            if (message is null)
            {
                _logger.LogError("Could not parse incoming message.");
                break;
            }

            _historyService.AddMessage(message);

            await SendToAllAsync(message, cancellationToken);

            receiveBuffer = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken);
        }

        await webSocket.CloseAsync(
            receiveBuffer.CloseStatus ?? WebSocketCloseStatus.InvalidMessageType, null, cancellationToken);

        _clients.RemoveDisconnectedClients();
    }
}
