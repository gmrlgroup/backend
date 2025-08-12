using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Application.Shared.Models;
using Application.Shared.Services;
using Application.Attributes;
using System.Security.Claims;
using System.Text.Json;

namespace Application.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IChatService _chatService;

    public ChatController(
        ILogger<ChatController> logger,
        IChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }    [HttpPost("send")]
    [RequireCompanyHeader]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            // Use the ChatService with Azure OpenAI integration
            var response = await _chatService.SendMessageAsync(request, userId, companyId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }    [HttpGet("history/{sessionId}")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<Application.Shared.Models.ChatMessage>>> GetChatHistory(string sessionId)
    {
        try
        {
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";
            
            // Use the ChatService to get chat history
            var history = await _chatService.GetChatHistoryAsync(sessionId, companyId);
            
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for session {SessionId}", sessionId);
            return StatusCode(500, "Internal server error");
        }
    }    [HttpGet("datasets/search")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<DatasetSearchResult>>> SearchDatasets([FromQuery] string query)
    {
        try
        {
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";
            var userId = Request.Headers["UserId"].ToString();
            
            // Use the ChatService to search datasets
            var datasets = await _chatService.SearchDatasetsAsync(query ?? "", companyId, userId);
            
            return Ok(datasets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching datasets with query '{Query}'", query);
            return StatusCode(500, "Internal server error");        }
    }
    [HttpGet("tables/search")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<TableSearchResult>>> SearchTables([FromQuery] string query)
    {
        try
        {
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";
            var userId = Request.Headers["UserId"].ToString();
            
            // Use the ChatService to search tables
            var tables = await _chatService.SearchTablesAsync(query ?? "", companyId, userId);
            
            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tables with query '{Query}'", query);
            return StatusCode(500, "Internal server error");
        }
    }
}
