using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using Application.Shared.Models;
using Application.Shared.Services.Data;

namespace Application.Shared.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId, string companyId);
    Task<List<Application.Shared.Models.ChatMessage>> GetChatHistoryAsync(string sessionId, string companyId);
    Task<List<DatasetSearchResult>> SearchDatasetsAsync(string query, string companyId, string userId);
    Task<List<TableSearchResult>> SearchTablesAsync(string query, string companyId, string userId);
}

public class ChatService : IChatService
{
    private readonly IDatasetService _datasetService;
    private readonly IDuckdbService _duckdbService;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILogger<ChatService> _logger;
    private readonly AzureOpenAIConfiguration _config;
    private readonly AzureOpenAIClient _openAIClient;

    public ChatService(
        IDatasetService datasetService,
        IDuckdbService duckdbService,
        IChatMessageRepository chatMessageRepository,
        ILogger<ChatService> logger,
        IOptions<AzureOpenAIConfiguration> config)
    {
        _datasetService = datasetService;
        _duckdbService = duckdbService;
        _chatMessageRepository = chatMessageRepository;
        _logger = logger;
        _config = config.Value;
        
        // Initialize Azure OpenAI client with error handling
        try
        {
            _openAIClient = new AzureOpenAIClient(
                new Uri(_config.Endpoint),
                new AzureKeyCredential(_config.ApiKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure OpenAI client. Check configuration.");
            throw;
        }
    }
    public async Task<ChatResponse> SendMessageAsync(ChatRequest request, string userId, string companyId)
    {
        try
        {
            // Generate session ID if not provided
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            // Save user message (TODO: implement repository)
            // await _chatMessageRepository.AddAsync(userMessage);

            // Get dataset context if datasets are referenced
            var datasetContext = await GetDatasetContextAsync(request.DatasetIds, companyId, userId);
            
            // Get table context if tables are referenced
            var tableContext = await GetTableContextAsync(request.TableReferences, companyId, userId);

            // Create system message with dataset and table context
            var systemMessage = BuildSystemMessage(datasetContext, tableContext);

            // Call Azure OpenAI with retry logic
            var aiResponse = await CallAzureOpenAIWithRetryAsync(systemMessage, request.Message);

            // check if AI response contains sql query
            // usually this would be done by checking the response content
            // but for simplicity, let's assume if it contains "```sql SELECT .... FROM ... ```" it's a query
            var isSqlQuery = aiResponse.Contains("```sql") && aiResponse.Contains("SELECT") && aiResponse.Contains("FROM");
            Console.WriteLine($"----- Reponse has query: {isSqlQuery}");
            if (isSqlQuery)
            {
                // If AI response is a SQL query, execute it against the dataset
                var sqlQuery = aiResponse.Substring(aiResponse.IndexOf("```sql") + 6).Trim();

                // remove the last  "```" if it exists
                if (sqlQuery.EndsWith("```"))
                {
                    sqlQuery = sqlQuery.Substring(0, sqlQuery.Length - 3).Trim();
                }
                // remove everything after the first ";" if it exists
                if (sqlQuery.Contains(";"))
                {
                    sqlQuery = sqlQuery.Substring(0, sqlQuery.IndexOf(";")).Trim();
                }

                // remove # fro mthe query
                sqlQuery = sqlQuery.Replace("#", string.Empty).Trim();

                // remove the database name if it exists after the FROM clause
                // we can assume the query is well-formed and has a FROM clause
                // and it has database name like "#database.table"
                if (sqlQuery.Contains("FROM"))
                {
                    var fromIndex = sqlQuery.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
                    var fromClause = sqlQuery.Substring(fromIndex + 4).Trim();
                    if (fromClause.Contains('.'))
                    {
                        // remove the database name but add space
                        sqlQuery = sqlQuery.Substring(0, fromIndex + 4) + " " + fromClause.Substring(fromClause.IndexOf('.') + 1).Trim();
                    }
                }


                Console.WriteLine($"Executing SQL Query: {sqlQuery}");

                // var dataset = await _datasetService.GetDatasetAsync(request.DatasetIds.FirstOrDefault() ?? string.Empty);
                var dataset = await _datasetService.GetDatasetAsync(request.TableReferences.First().DatasetId ?? string.Empty, userId);
                Console.WriteLine($"------------------------------- Dataset ID: {dataset?.Id}");
                // Func<IDataReader, T> mapFunction
                // var queryResult = await _duckdbService.ExecuteQueryAsync(dataset, sqlQuery, reader => reader.GetInt64(0));
                var queryResult = await _duckdbService.ExecuteQueryAsync(dataset, sqlQuery);
                // Format the result as a response message
                aiResponse = $"\n\nHere are the results of your query:\n{queryResult}";
            }

            // Get referenced datasets for response
            var referencedDatasets = new List<DatasetReference>();
            if (request.DatasetIds.Any())
            {
                var datasets = await _datasetService.GetDatasetsByIdsAsync(request.DatasetIds, companyId, userId);
                referencedDatasets = datasets.Select(d => new DatasetReference
                {
                    Id = d.Id!,
                    Name = d.Name!,
                    Description = d.Description!
                }).ToList();
            }

            // Get referenced tables for response
            var referencedTables = new List<TableReference>();
            if (request.TableReferences.Any())
            {
                referencedTables = await _datasetService.GetTablesByReferencesAsync(request.TableReferences, companyId, userId);
            }

            return new ChatResponse
            {
                Message = aiResponse,
                SessionId = sessionId,
                ReferencedDatasets = referencedDatasets,
                ReferencedTables = referencedTables,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message for user {UserId} in company {CompanyId}", userId, companyId);

            // Return a friendly error message instead of throwing
            return new ChatResponse
            {
                Message = "I'm sorry, I'm experiencing technical difficulties right now. Please try again in a few moments.",
                SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                ReferencedDatasets = new List<DatasetReference>(),
                ReferencedTables = new List<TableReference>(),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public Task<List<Application.Shared.Models.ChatMessage>> GetChatHistoryAsync(string sessionId, string companyId)
    {
        // TODO: Implement actual chat history retrieval from database
        return Task.FromResult(new List<Application.Shared.Models.ChatMessage>());
    }

    public async Task<List<DatasetSearchResult>> SearchDatasetsAsync(string query, string companyId, string userId)
    {
        var datasets = await _datasetService.SearchDatasetsAsync(query, companyId, userId);
        return datasets.Select(d => new DatasetSearchResult
        {
            Id = d.Id!,
            Name = d.Name!,
            Description = d.Description!,
            CompanyId = d.CompanyId
        }).ToList();
    }

    public async Task<List<TableSearchResult>> SearchTablesAsync(string query, string companyId, string userId)
    {
        return await _datasetService.SearchTablesAsync(query, companyId, userId);
    }

    // // TODO (CHAT): Implement actual dataset retrieval
    // private string GenerateMockResponse(string userMessage, List<string> datasetIds, string datasetContext)
    // {
    //     // Simple mock responses based on user input
    //     var message = userMessage.ToLowerInvariant();

    //     if (message.Contains("sales") || message.Contains("revenue"))
    //     {
    //         return "Based on the sales data, I can see that revenue has been trending upward this quarter. Would you like me to analyze specific metrics or time periods?";
    //     }
    //     else if (message.Contains("customer") || message.Contains("user"))
    //     {
    //         return "The customer analytics show interesting patterns in user behavior. I can help you dive deeper into specific customer segments or metrics.";
    //     }
    //     else if (datasetIds.Any())
    //     {
    //         return $"I can see you've referenced {datasetIds.Count} dataset(s). Let me analyze the data and provide insights based on your question: '{userMessage}'";
    //     }
    //     else
    //     {
    //         return $"I understand you're asking about: '{userMessage}'. To provide more specific insights, please reference a dataset using the # symbol.";
    //     }
    // }

    private async Task<string> GetDatasetContextAsync(List<string> datasetIds, string companyId, string userId)
    {
        if (!datasetIds.Any())
            return string.Empty;

        try
        {
            var datasets = await _datasetService.GetDatasetsByIdsAsync(datasetIds, companyId, userId);
            var context = string.Join("\n\n", datasets.Select(d =>
                $"Dataset: {d.Name}\nDescription: {d.Description}\nID: {d.Id}"));

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get dataset context for IDs: {DatasetIds}", string.Join(", ", datasetIds));
            return string.Empty;
        }
    }

    private async Task<string> GetTableContextAsync(List<TableReference> tableReferences, string companyId, string userId)
    {
        if (!tableReferences.Any())
            return string.Empty;

        try
        {
            var tablesWithData = await _datasetService.GetTablesByReferencesAsync(tableReferences, companyId, userId, sampleRows: 5);
            var context = new StringBuilder();

            foreach (var table in tablesWithData)
            {
                context.AppendLine($"Table: {table.DatasetName}.{table.TableName}");
                context.AppendLine($"Description: {table.Description}");
                context.AppendLine("Columns:");
                
                foreach (var column in table.Columns)
                {
                    context.AppendLine($"  - {column.Name} ({column.DataType}){(column.IsNullable ? ", nullable" : "")}");
                }
                
                context.AppendLine();
                
                if (table.SampleData.Any())
                {
                    context.AppendLine("Sample data (first 5 rows):");
                    
                    // Create a formatted table view of the sample data
                    var columnNames = table.Columns.Select(c => c.Name).ToList();
                    context.AppendLine(string.Join(" | ", columnNames));
                    context.AppendLine(string.Join(" | ", columnNames.Select(c => new string('-', Math.Max(c.Length, 8)))));
                    
                    foreach (var row in table.SampleData.Take(5))
                    {
                        var values = columnNames.Select(col => 
                        {
                            var value = row.ContainsKey(col) ? row[col]?.ToString() : "";
                            return string.IsNullOrEmpty(value) ? "NULL" : value;
                        }).ToList();
                        context.AppendLine(string.Join(" | ", values));
                    }
                }
                
                context.AppendLine();
                context.AppendLine("---");
                context.AppendLine();
            }

            return context.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get table context for references: {TableReferences}", 
                string.Join(", ", tableReferences.Select(t => $"{t.DatasetName}.{t.TableName}")));
            return string.Empty;
        }
    }

    /// <summary>
    /// Builds a system message with dataset context for the AI assistant
    /// </summary>
    private string BuildSystemMessage(string datasetContext, string tableContext)
    {
        Console.WriteLine(tableContext);
        var systemMessage = new StringBuilder();
        systemMessage.AppendLine("You are an AI assistant for a data analytics platform. You help users analyze and understand their data.");
        systemMessage.AppendLine("You should provide helpful, accurate, and relevant insights based on the available datasets.");
        systemMessage.AppendLine("Always be professional, concise, and focused on data-driven insights.");
        systemMessage.AppendLine("It is important that you do not use the name of the database if you are going to generate a query.");
        systemMessage.AppendLine("If there is a query make sure the query is for duckdb database.");
        systemMessage.AppendLine();

        if (!string.IsNullOrEmpty(datasetContext))
        {
            systemMessage.AppendLine("Available datasets for analysis:");
            systemMessage.AppendLine(datasetContext);
            systemMessage.AppendLine();
            systemMessage.AppendLine("Use the information about these datasets to provide relevant insights and recommendations.");
        }
        else
        {
            systemMessage.AppendLine("No specific datasets have been referenced. Provide general data analytics guidance and suggest using # to reference specific datasets for more targeted insights.");
        }

        if (!string.IsNullOrEmpty(tableContext))
        {
            systemMessage.AppendLine("Available tables for analysis:");
            systemMessage.AppendLine(tableContext);
            systemMessage.AppendLine();
            systemMessage.AppendLine("Use the information about these tables to provide relevant insights and recommendations.");
        }
        else
        {
            systemMessage.AppendLine("No specific tables have been referenced. Provide general guidance on table analysis and suggest referencing specific tables for more detailed insights.");
        }

        return systemMessage.ToString();
    }


    /// <summary>
    /// Calls Azure OpenAI with retry logic and error handling
    /// </summary>
    private async Task<string> CallAzureOpenAIWithRetryAsync(string systemMessage, string userMessage)
    {
        var retryCount = 0;
        Exception lastException = null!;

        while (retryCount <= _config.MaxRetryAttempts)
        {
            try
            {
                _logger.LogDebug("Calling Azure OpenAI API, attempt {Attempt}", retryCount + 1);

                // Get chat client for the deployment
                var chatClient = _openAIClient.GetChatClient(_config.DeploymentName);

                // Create chat messages
                var messages = new List<OpenAI.Chat.ChatMessage>
                {
                    new SystemChatMessage(systemMessage),
                    new UserChatMessage(userMessage)
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));

                var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cts.Token);

                var responseContent = response.Value.Content[0].Text;

                _logger.LogDebug("Successfully received response from Azure OpenAI");
                return responseContent ?? "I apologize, but I couldn't generate a response. Please try again.";
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                _logger.LogWarning(ex, "Azure OpenAI API call failed on attempt {Attempt}, retrying...", retryCount);

                if (retryCount <= _config.MaxRetryAttempts)
                {
                    // Exponential backoff: 1s, 2s, 4s, 8s...
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1));
                    await Task.Delay(delay);
                }
            }
        }

        _logger.LogError(lastException, "Failed to get response from Azure OpenAI after {MaxRetries} attempts", _config.MaxRetryAttempts);
        return "I'm sorry, I'm having trouble connecting to the AI service right now. Please try again later.";
    }
}

// Repository interface for chat messages
public interface IChatMessageRepository
{
    Task<Application.Shared.Models.ChatMessage> AddAsync(Application.Shared.Models.ChatMessage chatMessage);
    Task<List<Application.Shared.Models.ChatMessage>> GetBySessionIdAsync(string sessionId, string companyId);
    Task<List<Application.Shared.Models.ChatMessage>> GetByUserIdAsync(string userId, string companyId, int limit = 50);
}
