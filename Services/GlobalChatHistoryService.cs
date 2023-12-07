using System.Collections.Concurrent;
using System.Collections.Immutable;
using simple_chat.Controllers;

public class GlobalChatHistoryService
{
    private readonly ConcurrentBag<Message> _history
        = new ConcurrentBag<Message>();

    public void AddMessage(Message message)
    {
        _history.Add(message);
    }

    public IEnumerable<Message> GetLastMessages(int count = 50)
        => _history.Reverse().Take(count).ToImmutableList();
}
