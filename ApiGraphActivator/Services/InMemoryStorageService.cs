using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services;

public class InMemoryStorageService : ICrawlStorageService
{
    private readonly ILogger<InMemoryStorageService> _logger;
    private readonly List<DocumentInfo> _documents = new();
    private readonly List<ConversationSession> _sessions = new();
    private readonly List<Conversation> _conversations = new();
    private readonly List<ConversationMessage> _messages = new();
    private readonly object _lock = new object();

    public InMemoryStorageService(ILogger<InMemoryStorageService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
        _logger.LogInformation("In-memory storage service initialized");
    }

    public async Task TrackDocumentAsync(string companyName, string form, DateTime filingDate, string url)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                // Check if document already exists
                var existingDoc = _documents.FirstOrDefault(d => d.Url == url);
                if (existingDoc != null)
                {
                    // For recrawls, reset the processing status but keep the same ID
                    existingDoc.Processed = false;
                    existingDoc.ProcessedDate = null;
                    existingDoc.Success = true; // Reset to default
                    existingDoc.ErrorMessage = null;
                    _logger.LogTrace("Reset existing document for recrawl: {Url}", url);
                    return;
                }

                var document = new DocumentInfo
                {
                    Id = DocumentIdGenerator.GenerateDocumentId(url),
                    CompanyName = companyName,
                    Form = form,
                    FilingDate = filingDate,
                    Url = url,
                    Processed = false
                };

                _documents.Add(document);
                _logger.LogTrace("Tracked new document: {Company} - {Form} - {Url}", companyName, form, url);
            }
        });
    }

    public async Task MarkProcessedAsync(string url, bool success = true, string? errorMessage = null)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var document = _documents.FirstOrDefault(d => d.Url == url);
                if (document != null)
                {
                    document.Processed = true;
                    document.ProcessedDate = DateTime.UtcNow;
                    document.Success = success;
                    document.ErrorMessage = errorMessage;
                    _logger.LogTrace("Marked document as processed: {Url} - Success: {Success}", url, success);
                }
                else
                {
                    _logger.LogWarning("Document not found for processing: {Url}", url);
                }
            }
        });
    }

    public async Task<List<DocumentInfo>> GetUnprocessedAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _documents.Where(d => !d.Processed).ToList();
            }
        });
    }

    public async Task<CrawlMetrics> GetCrawlMetricsAsync(string? companyName = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var documents = _documents.AsEnumerable();
                
                if (!string.IsNullOrEmpty(companyName))
                {
                    documents = documents.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));
                }

                var docList = documents.ToList();
                
                return new CrawlMetrics
                {
                    CompanyName = companyName ?? "All Companies",
                    TotalDocuments = docList.Count,
                    ProcessedDocuments = docList.Count(d => d.Processed),
                    SuccessfulDocuments = docList.Count(d => d.Processed && d.Success),
                    FailedDocuments = docList.Count(d => d.Processed && !d.Success),
                    LastProcessedDate = docList.Where(d => d.ProcessedDate.HasValue).Max(d => d.ProcessedDate),
                    FormTypeCounts = docList.GroupBy(d => d.Form).ToDictionary(g => g.Key, g => g.Count())
                };
            }
        });
    }

    public async Task<List<ProcessingError>> GetProcessingErrorsAsync(string? companyName = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var errorDocs = _documents.Where(d => d.Processed && !d.Success && !string.IsNullOrEmpty(d.ErrorMessage));
                
                if (!string.IsNullOrEmpty(companyName))
                {
                    errorDocs = errorDocs.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));
                }

                return errorDocs.Select(d => new ProcessingError
                {
                    CompanyName = d.CompanyName,
                    Form = d.Form,
                    Url = d.Url,
                    ErrorMessage = d.ErrorMessage!,
                    ErrorDate = d.ProcessedDate ?? DateTime.UtcNow
                }).ToList();
            }
        });
    }

    public async Task<Dictionary<int, YearlyMetrics>> GetYearlyMetricsAsync()
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var yearlyMetrics = new Dictionary<int, YearlyMetrics>();

                foreach (var doc in _documents)
                {
                    var year = doc.FilingDate.Year;
                    if (!yearlyMetrics.ContainsKey(year))
                    {
                        yearlyMetrics[year] = new YearlyMetrics { Year = year };
                    }

                    var metrics = yearlyMetrics[year];
                    metrics.TotalDocuments++;
                    
                    if (doc.Processed)
                    {
                        metrics.ProcessedDocuments++;
                        if (doc.Success)
                            metrics.SuccessfulDocuments++;
                        else
                            metrics.FailedDocuments++;
                    }

                    // Track form types
                    if (!metrics.FormTypeCounts.ContainsKey(doc.Form))
                        metrics.FormTypeCounts[doc.Form] = 0;
                    metrics.FormTypeCounts[doc.Form]++;

                    // Track companies
                    if (!metrics.Companies.Contains(doc.CompanyName))
                        metrics.Companies.Add(doc.CompanyName);
                }

                return yearlyMetrics;
            }
        });
    }

    public async Task<Dictionary<int, YearlyMetrics>> GetCompanyYearlyMetricsAsync(string companyName)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var companyDocuments = _documents.Where(d => d.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));
                var yearlyMetrics = new Dictionary<int, YearlyMetrics>();

                foreach (var doc in companyDocuments)
                {
                    var year = doc.FilingDate.Year;
                    if (!yearlyMetrics.ContainsKey(year))
                    {
                        yearlyMetrics[year] = new YearlyMetrics { Year = year };
                    }

                    var metrics = yearlyMetrics[year];
                    metrics.TotalDocuments++;
                    
                    if (doc.Processed)
                    {
                        metrics.ProcessedDocuments++;
                        if (doc.Success)
                            metrics.SuccessfulDocuments++;
                        else
                            metrics.FailedDocuments++;
                    }

                    // Track form types
                    if (!metrics.FormTypeCounts.ContainsKey(doc.Form))
                        metrics.FormTypeCounts[doc.Form] = 0;
                    metrics.FormTypeCounts[doc.Form]++;

                    // Track companies
                    if (!metrics.Companies.Contains(doc.CompanyName))
                        metrics.Companies.Add(doc.CompanyName);
                }

                return yearlyMetrics;
            }
        });
    }

    public async Task<bool> IsHealthyAsync()
    {
        return await Task.FromResult(true);
    }

    public string GetStorageType() => "In-Memory Storage";

    public async Task<List<DocumentInfo>> SearchByCompanyAsync(string companyName, List<string>? formTypes = null, 
        DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var query = _documents.Where(d => 
                    d.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));

                if (formTypes?.Any() == true)
                {
                    query = query.Where(d => formTypes.Any(ft => 
                        d.Form.Equals(ft, StringComparison.OrdinalIgnoreCase)));
                }

                if (startDate.HasValue)
                {
                    query = query.Where(d => d.FilingDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(d => d.FilingDate <= endDate.Value);
                }

                return query
                    .OrderByDescending(d => d.FilingDate)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            }
        });
    }

    public async Task<List<DocumentInfo>> SearchByFormTypeAsync(List<string> formTypes, List<string>? companyNames = null,
        DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var query = _documents.Where(d => 
                    formTypes.Any(ft => d.Form.Equals(ft, StringComparison.OrdinalIgnoreCase)));

                if (companyNames?.Any() == true)
                {
                    query = query.Where(d => companyNames.Any(cn => 
                        d.CompanyName.Contains(cn, StringComparison.OrdinalIgnoreCase)));
                }

                if (startDate.HasValue)
                {
                    query = query.Where(d => d.FilingDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(d => d.FilingDate <= endDate.Value);
                }

                return query
                    .OrderByDescending(d => d.FilingDate)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            }
        });
    }

    public async Task<int> GetSearchResultCountAsync(string? companyName = null, List<string>? formTypes = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var query = _documents.AsQueryable();

                if (!string.IsNullOrEmpty(companyName))
                {
                    query = query.Where(d => d.CompanyName.Contains(companyName, StringComparison.OrdinalIgnoreCase));
                }

                if (formTypes?.Any() == true)
                {
                    query = query.Where(d => formTypes.Any(ft => 
                        d.Form.Equals(ft, StringComparison.OrdinalIgnoreCase)));
                }

                if (startDate.HasValue)
                {
                    query = query.Where(d => d.FilingDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(d => d.FilingDate <= endDate.Value);
                }

                return query.Count();
            }
        });
    }


    public async Task<DocumentInfo?> GetDocumentByIdAsync(string documentId)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _documents.FirstOrDefault(d => d.Id.Equals(documentId, StringComparison.OrdinalIgnoreCase));
            }
        });
    }

    public async Task<ConversationSession?> GetSessionAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _sessions.FirstOrDefault(s => s.Id == sessionId && 
                    (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow));
            }
        });
    }

    public async Task UpdateSessionAsync(ConversationSession session)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var existingSession = _sessions.FirstOrDefault(s => s.Id == session.Id);
                if (existingSession != null)
                {
                    var index = _sessions.IndexOf(existingSession);
                    _sessions[index] = session;
                    _logger.LogTrace("Updated session {SessionId}", session.Id);
                }
            }
        });
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                _sessions.RemoveAll(s => s.Id == sessionId);
                _conversations.RemoveAll(c => c.SessionId == sessionId);
                
                var conversationIds = _conversations.Where(c => c.SessionId == sessionId).Select(c => c.Id).ToList();
                foreach (var convId in conversationIds)
                {
                    _messages.RemoveAll(m => m.ConversationId == convId);
                }
                
                _logger.LogTrace("Deleted session {SessionId} and related conversations", sessionId);
            }
        });
    }

    public async Task<List<ConversationSession>> GetUserSessionsAsync(string userId)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _sessions.Where(s => s.UserId == userId && 
                    (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow)).ToList();
            }
        });
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var expiredSessions = _sessions.Where(s => s.ExpiresAt.HasValue && s.ExpiresAt < DateTime.UtcNow).ToList();
                
                foreach (var session in expiredSessions)
                {
                    _sessions.Remove(session);
                    _conversations.RemoveAll(c => c.SessionId == session.Id);
                    
                    var conversationIds = _conversations.Where(c => c.SessionId == session.Id).Select(c => c.Id).ToList();
                    foreach (var convId in conversationIds)
                    {
                        _messages.RemoveAll(m => m.ConversationId == convId);
                    }
                }
                
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
            }
        });
    }

    public async Task<Conversation> CreateConversationAsync(string sessionId, string? title = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var conversation = new Conversation
                {
                    SessionId = sessionId,
                    Title = title
                };

                _conversations.Add(conversation);
                _logger.LogTrace("Created conversation {ConversationId} in session {SessionId}", 
                    conversation.Id, sessionId);
                return conversation;
            }
        });
    }

    public async Task<Conversation?> GetConversationAsync(string conversationId)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _conversations.FirstOrDefault(c => c.Id == conversationId);
            }
        });
    }

    public async Task UpdateConversationAsync(Conversation conversation)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var existingConversation = _conversations.FirstOrDefault(c => c.Id == conversation.Id);
                if (existingConversation != null)
                {
                    var index = _conversations.IndexOf(existingConversation);
                    _conversations[index] = conversation;
                    _logger.LogTrace("Updated conversation {ConversationId}", conversation.Id);
                }
            }
        });
    }

    public async Task DeleteConversationAsync(string conversationId)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                _conversations.RemoveAll(c => c.Id == conversationId);
                _messages.RemoveAll(m => m.ConversationId == conversationId);
                _logger.LogTrace("Deleted conversation {ConversationId} and related messages", conversationId);
            }
        });
    }

    public async Task<List<Conversation>> GetSessionConversationsAsync(string sessionId)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _conversations.Where(c => c.SessionId == sessionId)
                    .OrderByDescending(c => c.LastMessageAt)
                    .ToList();
            }
        });
    }

    public async Task<ConversationMessage> AddMessageAsync(string conversationId, ConversationMessageRole role, 
        string content, List<DocumentCitation>? citations = null, Dictionary<string, object>? metadata = null)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var message = new ConversationMessage
                {
                    ConversationId = conversationId,
                    Role = role,
                    Content = content,
                    Citations = citations,
                    Metadata = metadata
                };

                _messages.Add(message);
                _logger.LogTrace("Added {Role} message to conversation {ConversationId}", 
                    role, conversationId);
                return message;
            }
        });
    }

    public async Task<List<ConversationMessage>> GetConversationMessagesAsync(string conversationId, int skip = 0, int take = 100)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                return _messages.Where(m => m.ConversationId == conversationId)
                    .OrderBy(m => m.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            }
        });
    }

    public async Task UpdateMessageAsync(ConversationMessage message)
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                var existingMessage = _messages.FirstOrDefault(m => m.Id == message.Id);
                if (existingMessage != null)
                {
                    var index = _messages.IndexOf(existingMessage);
                    _messages[index] = message;
                    _logger.LogTrace("Updated message {MessageId}", message.Id);
                }
            }
        });
    }
}
