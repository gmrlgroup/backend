using Application.Shared.Models;
using Application.Shared.Services;

namespace Application.Shared.Services.Data;

/// <summary>
/// In-memory implementation of chat message repository for development
/// TODO: Replace with actual database implementation
/// </summary>
public class InMemoryChatMessageRepository : IChatMessageRepository
{
    private readonly List<Application.Shared.Models.ChatMessage> _messages = new();
    private readonly object _lock = new object();    public Task<Application.Shared.Models.ChatMessage> AddAsync(Application.Shared.Models.ChatMessage chatMessage)
    {
        lock (_lock)
        {
            chatMessage.Id = Guid.NewGuid().ToString();
            chatMessage.CreatedAt = DateTime.UtcNow;
            _messages.Add(chatMessage);
        }

        return Task.FromResult(chatMessage);
    }

    public Task<List<Application.Shared.Models.ChatMessage>> GetBySessionIdAsync(string sessionId, string companyId)
    {
        lock (_lock)
        {
            var sessionMessages = _messages
                .Where(m => m.SessionId == sessionId && m.CompanyId == companyId)
                .OrderBy(m => m.CreatedAt)
                .ToList();

            return Task.FromResult(sessionMessages);
        }
    }

    public Task<List<Application.Shared.Models.ChatMessage>> GetByUserIdAsync(string userId, string companyId, int limit = 50)
    {
        lock (_lock)
        {
            var userMessages = _messages
                .Where(m => m.UserId == userId && m.CompanyId == companyId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(userMessages);
        }
    }
}
