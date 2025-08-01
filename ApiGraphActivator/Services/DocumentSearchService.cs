using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ApiGraphActivator.Services
{
    public class DocumentSearchService
    {
        private readonly ILogger<DocumentSearchService> _logger;
        private readonly StorageConfigurationService _storageConfigService;
        private readonly HttpClient _httpClient;

        public DocumentSearchService(
            ILogger<DocumentSearchService> logger,
            StorageConfigurationService storageConfigService)
        {
            _logger = logger;
            _storageConfigService = storageConfigService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");
        }

        /// <summary>
        /// Search for SEC documents based on criteria provided by Copilot Studio
        /// Checks tracked companies first, then queries SEC directly if needed
        /// </summary>
        public async Task<DocumentSearchResult> SearchDocumentsAsync(DocumentSearchRequest searchRequest)
        {
            try
            {
                _logger.LogInformation("Searching documents with query: {Query}, company: {Company}, formType: {FormType}, dateRange: {DateRange}", 
                    searchRequest.Query, searchRequest.Company, searchRequest.FormType, searchRequest.DateRange);

                // First, try to get results from tracked/crawled documents
                var trackedResults = await SearchTrackedDocumentsAsync(searchRequest);
                
                // If we have a specific company request and no tracked results, try SEC direct query
                if (!string.IsNullOrEmpty(searchRequest.Company) && 
                    (trackedResults == null || !trackedResults.Documents.Any()))
                {
                    _logger.LogInformation("No tracked documents found for company: {Company}. Attempting SEC direct query.", searchRequest.Company);
                    var secResults = await SearchSecDirectAsync(searchRequest);
                    
                    if (secResults.Success && secResults.Documents.Any())
                    {
                        return secResults;
                    }
                }

                return trackedResults ?? new DocumentSearchResult
                {
                    Success = false,
                    ErrorMessage = "No documents found",
                    Documents = new List<DocumentSearchResultItem>(),
                    SearchCriteria = searchRequest
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return new DocumentSearchResult
                {
                    Success = false,
                    ErrorMessage = $"Search error: {ex.Message}",
                    Documents = new List<DocumentSearchResultItem>(),
                    SearchCriteria = searchRequest
                };
            }
        }

        /// <summary>
        /// Search tracked/crawled documents from storage
        /// </summary>
        private async Task<DocumentSearchResult> SearchTrackedDocumentsAsync(DocumentSearchRequest searchRequest)
        {
            try
            {
                var storageService = await _storageConfigService.GetStorageServiceAsync();
                await storageService.InitializeAsync();

                // Get all documents and filter for processed ones
                var allDocuments = await storageService.GetUnprocessedAsync();
                // Note: We need to get both processed and unprocessed. For now, we'll work with unprocessed
                // and filter based on the Processed property
                
                // Apply filters based on search criteria
                var filteredDocuments = allDocuments.AsEnumerable();

                // Filter by company if specified
                if (!string.IsNullOrEmpty(searchRequest.Company))
                {
                    filteredDocuments = filteredDocuments.Where(doc => 
                        doc.CompanyName.Contains(searchRequest.Company, StringComparison.OrdinalIgnoreCase) ||
                        (doc.CompanyName.Contains(searchRequest.Company.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)));
                }

                // Filter by form type if specified
                if (!string.IsNullOrEmpty(searchRequest.FormType))
                {
                    filteredDocuments = filteredDocuments.Where(doc => 
                        doc.Form.Equals(searchRequest.FormType, StringComparison.OrdinalIgnoreCase));
                }

                // Filter by date range if specified
                if (!string.IsNullOrEmpty(searchRequest.DateRange))
                {
                    var (startDate, endDate) = ParseDateRange(searchRequest.DateRange);
                    if (startDate.HasValue && endDate.HasValue)
                    {
                        filteredDocuments = filteredDocuments.Where(doc => 
                            doc.FilingDate >= startDate.Value && doc.FilingDate <= endDate.Value);
                    }
                }

                // Convert to search results and sort by filing date (most recent first)
                var searchResults = filteredDocuments
                    .OrderByDescending(doc => doc.FilingDate)
                    .Take(20) // Limit to top 20 results
                    .Select(doc => new DocumentSearchResultItem
                    {
                        DocumentId = GenerateDocumentId(doc),
                        Company = doc.CompanyName,
                        FormType = doc.Form,
                        FilingDate = doc.FilingDate.ToString("yyyy-MM-dd"),
                        Title = $"{doc.CompanyName} - {doc.Form} - {doc.FilingDate:yyyy-MM-dd}",
                        Url = doc.Url,
                        Source = "Tracked"
                    })
                    .ToList();

                // Apply text search within results if query is provided
                if (!string.IsNullOrEmpty(searchRequest.Query))
                {
                    var queryTerms = searchRequest.Query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    searchResults = searchResults.Where(result => 
                        queryTerms.Any(term => 
                            result.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            result.Company.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            result.FormType.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }

                _logger.LogInformation("Found {Count} tracked documents matching search criteria", searchResults.Count);

                return new DocumentSearchResult
                {
                    Success = true,
                    TotalResults = searchResults.Count,
                    Documents = searchResults,
                    SearchCriteria = searchRequest
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tracked documents");
                return new DocumentSearchResult
                {
                    Success = false,
                    ErrorMessage = $"Tracked search error: {ex.Message}",
                    Documents = new List<DocumentSearchResultItem>(),
                    SearchCriteria = searchRequest
                };
            }
        }

        /// <summary>
        /// Search SEC EDGAR database directly for documents
        /// </summary>
        private async Task<DocumentSearchResult> SearchSecDirectAsync(DocumentSearchRequest searchRequest)
        {
            try
            {
                _logger.LogInformation("Performing direct SEC search for company: {Company}", searchRequest.Company);

                // First, get CIK for the company
                if (string.IsNullOrEmpty(searchRequest.Company))
                {
                    return new DocumentSearchResult
                    {
                        Success = false,
                        ErrorMessage = "Company name is required for SEC direct search",
                        Documents = new List<DocumentSearchResultItem>(),
                        SearchCriteria = searchRequest
                    };
                }

                var cik = await GetCompanyCikAsync(searchRequest.Company);
                if (string.IsNullOrEmpty(cik))
                {
                    return new DocumentSearchResult
                    {
                        Success = false,
                        ErrorMessage = $"Could not find CIK for company: {searchRequest.Company}",
                        Documents = new List<DocumentSearchResultItem>(),
                        SearchCriteria = searchRequest
                    };
                }

                // Get filings from SEC
                var filings = await GetSecFilingsAsync(cik, searchRequest);
                
                _logger.LogInformation("Found {Count} SEC documents for company: {Company}", filings.Count, searchRequest.Company);

                return new DocumentSearchResult
                {
                    Success = true,
                    TotalResults = filings.Count,
                    Documents = filings,
                    SearchCriteria = searchRequest
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing SEC direct search for company: {Company}", searchRequest.Company);
                return new DocumentSearchResult
                {
                    Success = false,
                    ErrorMessage = $"SEC search error: {ex.Message}",
                    Documents = new List<DocumentSearchResultItem>(),
                    SearchCriteria = searchRequest
                };
            }
        }

        /// <summary>
        /// Retrieve the full content of a specific document by its ID
        /// </summary>
        public async Task<DocumentContentResult> GetDocumentContentAsync(string documentId)
        {
            try
            {
                _logger.LogInformation("Getting document content for ID: {DocumentId}", documentId);

                // Parse the document ID to extract company name and URL
                var (companyName, url, formType, filingDate) = ParseDocumentId(documentId);
                
                if (string.IsNullOrEmpty(url))
                {
                    return new DocumentContentResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid document ID format"
                    };
                }

                // Try to get from tracked documents first
                var trackedContent = await GetTrackedDocumentContentAsync(documentId, companyName, url, formType, filingDate);
                if (trackedContent.Success)
                {
                    return trackedContent;
                }

                // If not found in tracked documents, try to fetch directly from SEC
                _logger.LogInformation("Document not found in tracked storage, fetching from SEC URL: {Url}", url);
                var directContent = await GetSecDocumentContentAsync(url, companyName, formType, filingDate);
                
                return directContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document content for ID: {DocumentId}", documentId);
                return new DocumentContentResult
                {
                    Success = false,
                    ErrorMessage = $"Document retrieval error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get document content from tracked/stored documents
        /// </summary>
        private async Task<DocumentContentResult> GetTrackedDocumentContentAsync(string documentId, string companyName, string url, string formType, DateTime filingDate)
        {
            try
            {
                var storageService = await _storageConfigService.GetStorageServiceAsync();
                await storageService.InitializeAsync();

                // Try to get the document content from storage
                var documents = await storageService.GetUnprocessedAsync();
                var document = documents.FirstOrDefault(doc => 
                    doc.Url.Equals(url, StringComparison.OrdinalIgnoreCase) &&
                    doc.CompanyName.Equals(companyName, StringComparison.OrdinalIgnoreCase));

                if (document == null)
                {
                    return new DocumentContentResult
                    {
                        Success = false,
                        ErrorMessage = $"Document not found in tracked storage"
                    };
                }

                // Get the actual stored content
                var content = GetStoredDocumentContentAsync(document);

                return new DocumentContentResult
                {
                    Success = true,
                    DocumentId = documentId,
                    Company = document.CompanyName,
                    FormType = document.Form,
                    FilingDate = document.FilingDate.ToString("yyyy-MM-dd"),
                    Title = $"{document.CompanyName} - {document.Form} - {document.FilingDate:yyyy-MM-dd}",
                    Content = content,
                    Url = document.Url,
                    Source = "Tracked"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tracked document content");
                return new DocumentContentResult
                {
                    Success = false,
                    ErrorMessage = $"Tracked document error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get document content directly from SEC URL
        /// </summary>
        private async Task<DocumentContentResult> GetSecDocumentContentAsync(string url, string companyName, string formType, DateTime filingDate)
        {
            try
            {
                _logger.LogInformation("Fetching document content from SEC URL: {Url}", url);

                var content = await FetchSecDocumentWithBackoff(url);
                
                if (string.IsNullOrEmpty(content))
                {
                    return new DocumentContentResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to fetch document content from SEC"
                    };
                }

                // Process content based on document type
                string processedContent;
                if (url.Contains(".pdf"))
                {
                    // For PDF, we would need to convert to bytes and extract text
                    processedContent = "PDF document content extraction not implemented for direct SEC access";
                }
                else
                {
                    // Process HTML document
                    processedContent = ProcessHtmlContent(content);
                }

                return new DocumentContentResult
                {
                    Success = true,
                    DocumentId = GenerateDocumentIdFromUrl(url, companyName, formType, filingDate),
                    Company = companyName,
                    FormType = formType,
                    FilingDate = filingDate.ToString("yyyy-MM-dd"),
                    Title = $"{companyName} - {formType} - {filingDate:yyyy-MM-dd}",
                    Content = processedContent,
                    Url = url,
                    Source = "SEC Direct"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document from SEC URL: {Url}", url);
                return new DocumentContentResult
                {
                    Success = false,
                    ErrorMessage = $"SEC fetch error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get a list of recent documents for a specific company
        /// </summary>
        public async Task<DocumentSearchResult> GetRecentDocumentsByCompanyAsync(string companyName, int maxResults = 10)
        {
            var searchRequest = new DocumentSearchRequest
            {
                Query = "",
                Company = companyName,
                FormType = null,
                DateRange = null
            };

            var result = await SearchDocumentsAsync(searchRequest);
            
            // Limit results
            if (result.Success && result.Documents.Count > maxResults)
            {
                result.Documents = result.Documents.Take(maxResults).ToList();
                result.TotalResults = maxResults;
            }

            return result;
        }

        /// <summary>
        /// Get the latest earnings document (10-Q or 10-K) for a company
        /// </summary>
        public async Task<DocumentSearchResult> GetLatestEarningsDocumentAsync(string companyName)
        {
            try
            {
                // Try to find the most recent 10-Q (quarterly) first
                var quarterlySearch = new DocumentSearchRequest
                {
                    Query = "",
                    Company = companyName,
                    FormType = "10-Q",
                    DateRange = null
                };

                var quarterlyResult = await SearchDocumentsAsync(quarterlySearch);
                
                if (quarterlyResult.Success && quarterlyResult.Documents.Any())
                {
                    // Return the most recent quarterly report
                    quarterlyResult.Documents = quarterlyResult.Documents.Take(1).ToList();
                    quarterlyResult.TotalResults = 1;
                    return quarterlyResult;
                }

                // If no 10-Q found, try 10-K (annual)
                var annualSearch = new DocumentSearchRequest
                {
                    Query = "",
                    Company = companyName,
                    FormType = "10-K",
                    DateRange = null
                };

                var annualResult = await SearchDocumentsAsync(annualSearch);
                
                if (annualResult.Success && annualResult.Documents.Any())
                {
                    annualResult.Documents = annualResult.Documents.Take(1).ToList();
                    annualResult.TotalResults = 1;
                }

                return annualResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest earnings document for company: {Company}", companyName);
                return new DocumentSearchResult
                {
                    Success = false,
                    ErrorMessage = $"Error retrieving latest earnings: {ex.Message}",
                    Documents = new List<DocumentSearchResultItem>()
                };
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Get CIK for a company from SEC company tickers API
        /// </summary>
        private async Task<string> GetCompanyCikAsync(string companyName)
        {
            try
            {
                _logger.LogInformation("Looking up CIK for company: {CompanyName}", companyName);

                // First check if it's in our tracked companies
                var trackedCik = await GetTrackedCompanyCikAsync(companyName);
                if (!string.IsNullOrEmpty(trackedCik))
                {
                    return trackedCik;
                }

                // Fallback to SEC API lookup
                var url = "https://www.sec.gov/files/company_tickers.json";
                var response = await _httpClient.GetStringAsync(url);
                
                var jsonDocument = JsonDocument.Parse(response);
                foreach (var item in jsonDocument.RootElement.EnumerateObject())
                {
                    var company = item.Value;
                    var title = company.GetProperty("title").GetString() ?? "";
                    var ticker = company.GetProperty("ticker").GetString() ?? "";
                    
                    if (title.Contains(companyName, StringComparison.OrdinalIgnoreCase) ||
                        ticker.Equals(companyName, StringComparison.OrdinalIgnoreCase) ||
                        companyName.Contains(title, StringComparison.OrdinalIgnoreCase))
                    {
                        return company.GetProperty("cik_str").ToString();
                    }
                }

                _logger.LogWarning("Could not find CIK for company: {CompanyName}", companyName);
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up CIK for company: {CompanyName}", companyName);
                return "";
            }
        }

        /// <summary>
        /// Get CIK from tracked companies configuration
        /// </summary>
        private async Task<string> GetTrackedCompanyCikAsync(string companyName)
        {
            try
            {
                var config = await ConfigurationService.LoadCrawledCompaniesAsync();
                if (config?.Companies != null)
                {
                    var company = config.Companies.FirstOrDefault(c => 
                        c.Title.Contains(companyName, StringComparison.OrdinalIgnoreCase) ||
                        c.Ticker.Equals(companyName, StringComparison.OrdinalIgnoreCase) ||
                        companyName.Contains(c.Title, StringComparison.OrdinalIgnoreCase));
                    
                    if (company != null)
                    {
                        _logger.LogInformation("Found tracked company CIK: {CIK} for {CompanyName}", company.Cik, companyName);
                        return company.Cik.ToString();
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking tracked companies for: {CompanyName}", companyName);
                return "";
            }
        }

        /// <summary>
        /// Get SEC filings for a company by CIK
        /// </summary>
        private async Task<List<DocumentSearchResultItem>> GetSecFilingsAsync(string cik, DocumentSearchRequest searchRequest)
        {
            try
            {
                var paddedCik = cik.PadLeft(10, '0');
                var url = $"https://data.sec.gov/submissions/CIK{paddedCik}.json";
                
                _logger.LogInformation("Fetching SEC filings from: {Url}", url);
                
                var response = await _httpClient.GetStringAsync(url);
                var jsonDocument = JsonDocument.Parse(response);
                
                var root = jsonDocument.RootElement;
                var filings = root.GetProperty("filings");
                var recentFilings = filings.GetProperty("recent");
                
                var accessionNumbers = recentFilings.GetProperty("accessionNumber");
                var forms = recentFilings.GetProperty("form");
                var primaryDocuments = recentFilings.GetProperty("primaryDocument");
                var reportDates = recentFilings.GetProperty("reportDate");
                
                var results = new List<DocumentSearchResultItem>();
                
                // Log array lengths for debugging
                _logger.LogInformation("SEC API returned arrays - Forms: {FormsCount}, Dates: {DatesCount}, AccessionNumbers: {AccessionCount}", 
                    forms.GetArrayLength(), reportDates.GetArrayLength(), accessionNumbers.GetArrayLength());
                
                // Parse date range for filtering
                var (startDate, endDate) = ParseDateRange(searchRequest.DateRange ?? "");
                
                // Get years of data configuration (similar to EdgarService)
                var yearsOfData = await DataCollectionConfigurationService.GetYearsOfDataAsync();
                var cutoffDate = DateTime.Now.AddYears(-yearsOfData);
                
                _logger.LogInformation("Date filtering - Years of data: {Years}, Cutoff date: {CutoffDate}", yearsOfData, cutoffDate);
                
                for (int i = 0; i < forms.GetArrayLength(); i++)
                {
                    try
                    {
                        var reportDateStr = reportDates[i].GetString();
                        _logger.LogDebug("Processing filing {Index}: form={Form}, date={Date}", i, forms[i].GetString(), reportDateStr);
                        
                        if (string.IsNullOrWhiteSpace(reportDateStr) || reportDateStr.Length < 8)
                        {
                            _logger.LogDebug("Skipping filing at index {Index} due to invalid date: '{Date}'", i, reportDateStr);
                            continue;
                        }
                            
                        if (!DateTime.TryParse(reportDateStr, out var reportDate))
                        {
                            _logger.LogDebug("Skipping filing at index {Index} due to unparseable date: '{Date}'", i, reportDateStr);
                            continue;
                        }
                        
                        // Skip if too old
                        if (reportDate < cutoffDate)
                        {
                            _logger.LogDebug("Skipping filing at index {Index} due to date too old: {Date} < {CutoffDate}", i, reportDate, cutoffDate);
                            continue;
                        }
                            
                        // Apply date range filter if specified
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            if (reportDate < startDate.Value || reportDate > endDate.Value)
                                continue;
                        }
                        
                        var form = forms[i].GetString() ?? "";
                        var accessionNumber = accessionNumbers[i].GetString() ?? "";
                        var primaryDoc = primaryDocuments[i].GetString() ?? "";
                        
                        // Apply form type filter if specified
                        if (!string.IsNullOrEmpty(searchRequest.FormType))
                        {
                            if (!form.Equals(searchRequest.FormType, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogDebug("Skipping filing at index {Index} due to form type filter: {Form} != {RequestedForm}", i, form, searchRequest.FormType);
                                continue;
                            }
                        }
                        
                        // Build SEC URL
                        var cleanAccessionNumber = accessionNumber.Replace("-", "");
                        var documentUrl = $"https://www.sec.gov/Archives/edgar/data/{cik}/{cleanAccessionNumber}/{primaryDoc}";
                        
                        _logger.LogDebug("Adding filing to results: {Form} - {Date} - {Url}", form, reportDate, documentUrl);
                        
                        var resultItem = new DocumentSearchResultItem
                        {
                            DocumentId = GenerateDocumentIdFromUrl(documentUrl, searchRequest.Company ?? "", form, reportDate),
                            Company = searchRequest.Company ?? "",
                            FormType = form,
                            FilingDate = reportDate.ToString("yyyy-MM-dd"),
                            Title = $"{searchRequest.Company} - {form} - {reportDate:yyyy-MM-dd}",
                            Url = documentUrl,
                            Source = "SEC Direct"
                        };
                        
                        results.Add(resultItem);
                        
                        // Limit results
                        if (results.Count >= 20)
                            break;
                            
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing filing at index {Index}", i);
                        continue;
                    }
                }
                
                _logger.LogInformation("Collected {PreFilterCount} filings before query filtering", results.Count);
                
                // Apply text query filter if specified
                // For SEC direct searches, only apply query filtering if it's a form-type related query
                if (!string.IsNullOrEmpty(searchRequest.Query))
                {
                    var queryTerms = searchRequest.Query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    
                    // Check if this is a form-type specific query (like "10-K", "10-Q", "8-K")
                    var formTypeQuery = queryTerms.Any(term => 
                        term.Contains("10-k") || term.Contains("10-q") || term.Contains("8-k") || 
                        term.Contains("proxy") || term.Contains("annual") || term.Contains("quarterly"));
                    
                    if (formTypeQuery)
                    {
                        // Apply strict filtering for form-type queries
                        results = results.Where(result => 
                            queryTerms.Any(term => 
                                result.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                result.Company.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                result.FormType.Contains(term, StringComparison.OrdinalIgnoreCase)))
                            .ToList();
                            
                        _logger.LogInformation("After form-type query filtering: {PostFilterCount} filings remain", results.Count);
                    }
                    else
                    {
                        // For general content queries (like "revenue"), don't filter SEC results since we can't search content
                        // Instead, return all results and let the user browse through them
                        _logger.LogInformation("General content query detected - returning all SEC filings for browsing: {Count} filings", results.Count);
                    }
                }
                
                // Sort by filing date (most recent first)
                results = results.OrderByDescending(r => r.FilingDate).ToList();
                
                _logger.LogInformation("Found {Count} SEC filings for CIK: {CIK}", results.Count, cik);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching SEC filings for CIK: {CIK}", cik);
                return new List<DocumentSearchResultItem>();
            }
        }

        /// <summary>
        /// Fetch document content from SEC with exponential backoff
        /// </summary>
        private async Task<string> FetchSecDocumentWithBackoff(string url)
        {
            const int maxRetries = 3;
            var delay = TimeSpan.FromSeconds(1);
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Fetching SEC document (attempt {Attempt}): {Url}", attempt, url);
                    
                    var response = await _httpClient.GetStringAsync(url);
                    _logger.LogDebug("Successfully fetched SEC document: {Url}", url);
                    return response;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to fetch SEC document (attempt {Attempt}): {Url}. Retrying in {Delay}ms", 
                        attempt, url, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching SEC document (attempt {Attempt}): {Url}", attempt, url);
                    if (attempt == maxRetries)
                        throw;
                }
            }
            
            return "";
        }

        /// <summary>
        /// Process HTML content to extract clean text
        /// </summary>
        private string ProcessHtmlContent(string htmlContent)
        {
            try
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);
                
                // Extract text content and clean it up
                var textContent = htmlDoc.DocumentNode.InnerText;
                
                // Decode HTML entities (like &amp;, &lt;, &gt;, etc.)
                textContent = System.Net.WebUtility.HtmlDecode(textContent);
                
                // Remove XML declarations and processing instructions
                textContent = Regex.Replace(textContent, @"<\?xml[^>]*\?>", "", RegexOptions.IgnoreCase);
                textContent = Regex.Replace(textContent, @"<\?[^>]*\?>", "", RegexOptions.IgnoreCase);
                
                // Remove checkbox symbols and other special Unicode characters
                textContent = textContent.Replace("☐", ""); // Empty checkbox
                textContent = textContent.Replace("☑", ""); // Checked checkbox
                textContent = textContent.Replace("☒", ""); // X-marked checkbox
                textContent = textContent.Replace("✓", ""); // Checkmark
                textContent = textContent.Replace("✗", ""); // X mark
                
                // Remove XBRL namespace declarations and technical metadata
                textContent = Regex.Replace(textContent, @"iso4217:\w+", "", RegexOptions.IgnoreCase);
                textContent = Regex.Replace(textContent, @"xbrli:\w+", "", RegexOptions.IgnoreCase);
                textContent = Regex.Replace(textContent, @"\b\d{10}\b", ""); // Remove 10-digit numbers that look like IDs
                
                // Clean up whitespace and formatting
                textContent = Regex.Replace(textContent, @"\s+", " ");
                textContent = textContent.Trim();
                
                // Limit content length for performance
                if (textContent.Length > 100000)
                {
                    textContent = textContent.Substring(0, 100000) + "\n\n[Content truncated for performance]";
                }
                
                return textContent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing HTML content");
                return "Error processing document content";
            }
        }

        /// <summary>
        /// Get stored document content from the storage system
        /// </summary>
        private string GetStoredDocumentContentAsync(DocumentInfo document)
        {
            try
            {
                // TODO: Implement actual document content retrieval from blob storage
                // This would typically involve:
                // 1. Getting the document from blob storage using document.Id or document.Url
                // 2. Extracting text content if it's a PDF using PdfProcessingService
                // 3. Returning the formatted content

                // For now, return a placeholder with metadata
                return $"""
                SEC DOCUMENT: {document.Form}
                COMPANY: {document.CompanyName}
                FILING DATE: {document.FilingDate:yyyy-MM-dd}
                DOCUMENT URL: {document.Url}
                
                [Document content would be retrieved from your storage system here]
                
                This document was processed on: {document.ProcessedDate:yyyy-MM-dd HH:mm:ss}
                Processed successfully: {document.Success}
                """;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve stored document content for {Company} {Form}", 
                    document.CompanyName, document.Form);
                
                return $"Error retrieving stored document content: {ex.Message}";
            }
        }

        /// <summary>
        /// Generate document ID from URL and metadata
        /// </summary>
        private string GenerateDocumentIdFromUrl(string url, string companyName, string formType, DateTime filingDate)
        {
            var idData = new
            {
                company = companyName,
                form = formType,
                date = filingDate.ToString("yyyy-MM-dd"),
                url = url
            };

            var json = JsonSerializer.Serialize(idData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Parse date range string into start and end dates
        /// Expected formats: "2024-01-01 to 2024-12-31", "last 30 days", "2024"
        /// </summary>
        private (DateTime? startDate, DateTime? endDate) ParseDateRange(string dateRange)
        {
            try
            {
                if (string.IsNullOrEmpty(dateRange))
                    return (null, null);

                dateRange = dateRange.ToLower().Trim();

                // Handle "YYYY to YYYY" or "YYYY-MM-DD to YYYY-MM-DD"
                if (dateRange.Contains(" to "))
                {
                    var parts = dateRange.Split(" to ");
                    if (parts.Length == 2)
                    {
                        if (DateTime.TryParse(parts[0], out var start) && DateTime.TryParse(parts[1], out var end))
                        {
                            return (start, end);
                        }
                    }
                }

                // Handle "last X days"
                if (dateRange.StartsWith("last ") && dateRange.EndsWith(" days"))
                {
                    var daysStr = dateRange.Replace("last ", "").Replace(" days", "");
                    if (int.TryParse(daysStr, out var days))
                    {
                        return (DateTime.Now.AddDays(-days), DateTime.Now);
                    }
                }

                // Handle single year "2024"
                if (int.TryParse(dateRange, out var year) && year >= 2000 && year <= 2030)
                {
                    return (new DateTime(year, 1, 1), new DateTime(year, 12, 31));
                }

                // Handle single date
                if (DateTime.TryParse(dateRange, out var singleDate))
                {
                    return (singleDate, singleDate.AddDays(1));
                }

                return (null, null);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Generate a unique document ID from document metadata (legacy format for tracked documents)
        /// </summary>
        private string GenerateDocumentId(DocumentInfo document)
        {
            // Use new format with full URL for consistency
            return GenerateDocumentIdFromUrl(document.Url, document.CompanyName, document.Form, document.FilingDate);
        }

        /// <summary>
        /// Parse a document ID back into its components
        /// </summary>
        private (string companyName, string url, string formType, DateTime filingDate) ParseDocumentId(string documentId)
        {
            try
            {
                var bytes = Convert.FromBase64String(documentId);
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var idData = JsonSerializer.Deserialize<JsonElement>(json);

                var companyName = idData.GetProperty("company").GetString() ?? "";
                var formType = idData.GetProperty("form").GetString() ?? "";
                var dateStr = idData.GetProperty("date").GetString() ?? "";
                
                DateTime.TryParse(dateStr, out var filingDate);

                // Try to get full URL if available (new format)
                string url = "";
                if (idData.TryGetProperty("url", out var urlElement))
                {
                    url = urlElement.GetString() ?? "";
                }
                else
                {
                    // Legacy format with URL hash - we'll need to look it up
                    var urlHash = idData.GetProperty("urlHash").GetString() ?? "";
                    // For legacy format, we can't recover the full URL from hash
                    // We would need to search storage to find matching document
                    url = ""; // Will trigger lookup in calling method
                }

                return (companyName, url, formType, filingDate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing document ID: {DocumentId}", documentId);
                return ("", "", "", DateTime.MinValue);
            }
        }

        #endregion
    }

    #region Data Models

    public class DocumentSearchRequest
    {
        public string Query { get; set; } = "";
        public string? Company { get; set; }
        public string? FormType { get; set; }
        public string? DateRange { get; set; }
    }

    public class DocumentSearchResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalResults { get; set; }
        public List<DocumentSearchResultItem> Documents { get; set; } = new();
        public DocumentSearchRequest? SearchCriteria { get; set; }
    }

    public class DocumentSearchResultItem
    {
        public string DocumentId { get; set; } = "";
        public string Company { get; set; } = "";
        public string FormType { get; set; } = "";
        public string FilingDate { get; set; } = "";
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Source { get; set; } = ""; // "Tracked" or "SEC Direct"
    }

    public class DocumentContentResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string DocumentId { get; set; } = "";
        public string Company { get; set; } = "";
        public string FormType { get; set; } = "";
        public string FilingDate { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Url { get; set; } = "";
        public string Source { get; set; } = ""; // "Tracked" or "SEC Direct"
    }

    #endregion
}
