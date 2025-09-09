using System;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;
using System.Reflection.Metadata;
using Azure.Storage.Blobs;
using HtmlAgilityPack;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ExternalConnectors;

namespace ApiGraphActivator.Services;

public static class EdgarService
{
    // Define static members for HttpClient, LoggingService, TableClient, and various configuration strings
    private static readonly HttpClient _client;
    private static ILogger? _logger;
    private static TableClient? _tableClient;
    private static BlobServiceClient? _blobServiceClient;
    private static ICrawlStorageService? _storageService;
    static string? connectionString;
    static string? companyName;
    static string? companySymbol;
    static string? processedBlobContainerName;
    static string cikLookup = "";
    static string cik = "";
    static string? companyTableName;
    static string? processedTableName;
    static public string itemId { get; set; } = "";
    static public string titleField { get; set; } = "";
    static public string urlField { get; set; } = "";
    static public string formField { get; set; } = "";
    static public string companyField { get; set; } = "";
    static public DateTime? reportDateField { get; set; } = DateTime.MinValue;

    // Static constructor to initialize static members
    static EdgarService()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("User-Agent", $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress") ?? "unknown@example.com"})");
        _logger?.LogTrace("HttpClient initialized with User-Agent header.");

        connectionString = Environment.GetEnvironmentVariable("TableStorage");
        companyTableName = Environment.GetEnvironmentVariable("CompanyTableName");
        processedTableName = Environment.GetEnvironmentVariable("ProcessedTableName");
        processedBlobContainerName = Environment.GetEnvironmentVariable("BlobContainerName");
        
        // Only initialize Azure services if connection string is provided
        if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(processedTableName))
        {
            try
            {
                _tableClient = new TableClient(connectionString, processedTableName);
                _blobServiceClient = new BlobServiceClient(connectionString);
            }
            catch (Exception)
            {
                // Azure services not available - continue without them
                _tableClient = null;
                _blobServiceClient = null;
            }
        }
    }

    public static bool IsBase64String(string base64)
    {
        if (string.IsNullOrEmpty(base64))
            return false;

        Span<byte> buffer = new Span<byte>(new byte[base64.Length]);
        return Convert.TryFromBase64String(base64, buffer, out _);
    }

    // Define an asynchronous static method to hydrate lookup data from EdgarService
    async public static Task<List<EdgarExternalItem>> HydrateLookupData()
    {
        string url = "https://www.sec.gov/files/company_tickers.json";
        var company_tkr_response = await _client.GetStringAsync(url).ConfigureAwait(false);

        // Create a list to store the table entities and filing documents
        List<TableEntity> tableEntities = new List<TableEntity>();
        List<EdgarExternalItem> filingDocuments = new List<EdgarExternalItem>();

        try
        {
            // Only query table if Azure Table Storage is available
            if (_tableClient != null && !string.IsNullOrEmpty(companyTableName))
            {
                TableClient tc = new TableClient(connectionString, companyTableName);
                // Retrieve all entities from the table
                tableEntities = tc.Query<TableEntity>().ToList();
            }
            else
            {
                _logger?.LogInformation("Azure Table Storage not configured. Skipping table-based company lookup.");
                return filingDocuments;
            }

            // Process the table entities as needed
            foreach (var entity in tableEntities)
            {
                _logger?.LogTrace($"PartitionKey: {entity.PartitionKey}, RowKey: {entity.RowKey}");
                companyName = entity.GetString("RowKey").Trim();
                companySymbol = entity.GetString("Symbol").Trim();
                cikLookup = ExtractCIK(company_tkr_response, companySymbol);

                if (cikLookup == "Company not found")
                {
                    _logger?.LogError($"Company not found for {companySymbol}");
                    continue;
                }

                string filingJson = await GetCIKFiling().ConfigureAwait(false);
                filingDocuments = await GetDocument(filingJson).ConfigureAwait(false);
                
                // Iterate over each item in the content list and transform it
                // foreach (var item in filingDocuments)
                // {
                //     ContentService.Transform(item);
                // }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error retrieving entities from table: {ex.Message}");
            return filingDocuments;
        }

        return filingDocuments;
    }

    // Static field to track companies with processed documents in current session
    private static HashSet<string> _companiesWithProcessedDocuments = new HashSet<string>();

    // Define an asynchronous static method to hydrate lookup data for specific companies
    async public static Task<List<EdgarExternalItem>> HydrateLookupDataForCompanies(List<Company> companies, string? connectionId = null)
    {
        _logger?.LogInformation("Processing {CompanyCount} companies", companies.Count);
        
        // Clear the tracking set for this session
        _companiesWithProcessedDocuments.Clear();
        
        // Create a list to store filing documents
        List<EdgarExternalItem> filingDocuments = new List<EdgarExternalItem>();

        try
        {
            // Process each selected company
            foreach (var company in companies)
            {
                _logger?.LogTrace($"Processing company: {company.Ticker} - {company.Title}");
                
                companyName = company.Title.Trim();
                companySymbol = company.Ticker.Trim();
                cikLookup = company.Cik.ToString();

                string filingJson = await GetCIKFiling().ConfigureAwait(false);
                var companyFilingDocuments = await GetDocument(filingJson, connectionId).ConfigureAwait(false);
                
                if (companyFilingDocuments != null)
                {
                    filingDocuments.AddRange(companyFilingDocuments);
                }
                
                _logger?.LogTrace($"Processed {companyFilingDocuments?.Count ?? 0} documents for {company.Ticker}");
            }
            
            // Update timestamps only for companies that actually had documents processed
            if (_companiesWithProcessedDocuments.Any())
            {
                var processedCompanies = companies.Where(c => _companiesWithProcessedDocuments.Contains(c.Title)).ToList();
                await ConfigurationService.UpdateCrawledCompanyTimestampsAsync(processedCompanies, connectionId);
                _logger?.LogInformation("Updated timestamps for {ProcessedCount} companies that had documents processed in connection {ConnectionId}", processedCompanies.Count, connectionId ?? "default");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing companies: {ex.Message}");
            return filingDocuments;
        }

        _logger?.LogInformation("Completed processing. Total documents: {DocumentCount}, Companies with processed documents: {ProcessedCount}", 
            filingDocuments.Count, _companiesWithProcessedDocuments.Count);
        return filingDocuments;
    }

    // Define a method to extract CIK from JSON response
    static string ExtractCIK(string json, string companySymbol)
    {
        var jsonObject = JsonDocument.Parse(json).RootElement;
        foreach (var item in jsonObject.EnumerateObject())
        {
            var company = item.Value;
            if (company.GetProperty("ticker").GetString().Trim().Equals(companySymbol, StringComparison.OrdinalIgnoreCase))
            {
                return company.GetProperty("cik_str").ToString();
            }
        }
        return "Company not found";
    }

    // Define an asynchronous static method to get CIK filing
    async public static Task<string> GetCIKFiling()
    {
        if (string.IsNullOrEmpty(cikLookup))
        {
            _logger.LogTrace($"CIK not populated. Skipping.");
            return "";
        }
        cik = cikLookup;
        cikLookup = cikLookup.PadLeft(10, '0');
        _logger.LogTrace($"CIK for {companyName}, {companySymbol}: {cikLookup}");
        if (cikLookup.Equals("Company not found"))
        {
            _logger.LogTrace($"CIK for {companyName}, {companySymbol} not found. Skipping.");
            return "";
        }

        // Hit API endpoint to get the filings for the CIK retrieved
        _logger.LogTrace($"Fetching JSON payload for https://data.sec.gov/submissions/CIK{cikLookup}.json");
        var filingString = await _client.GetStringAsync($"https://data.sec.gov/submissions/CIK{cikLookup}.json").ConfigureAwait(false);
        _logger.LogTrace($"JSON payload for https://data.sec.gov/submissions/CIK{cikLookup}.json retrieved.");
        return filingString;
    }

    // Define an asynchronous static method to get document from filing string
    async public static Task<List<EdgarExternalItem>> GetDocument(string filingString, string? connectionId = null)
    {
        string retVal = "";
        List<EdgarExternalItem> externalItemData = new List<EdgarExternalItem>();
        using (JsonDocument doc = JsonDocument.Parse(filingString))
        {
            JsonElement root = doc.RootElement;

            // Access the "filings" element
            JsonElement filings = root.GetProperty("filings");

            // Access the "recent" element within "filings"
            JsonElement recentFilings = filings.GetProperty("recent");

            var aNumberObj = recentFilings.GetProperty("accessionNumber");
            var formsObj = recentFilings.GetProperty("form");
            var pDocObj = recentFilings.GetProperty("primaryDocument");
            var reportDate = recentFilings.GetProperty("reportDate");

            // Iterate through the recent filings
            for (int i = 0; i < formsObj.GetArrayLength(); i++)
            {
                try
                {
                    if (reportDate[i].ToString().Length < 10)
                    {
                        continue;
                    }
                    var theDate = DateTime.Parse(reportDate[i].ToString());
                    int yearsOfData = await DataCollectionConfigurationService.GetYearsOfDataAsync();

                    // Check if the date is more than the configured years in the past
                    if (theDate < DateTime.Now.AddYears(-yearsOfData))
                    {
                        continue;
                    }

                    var form = formsObj[i].ToString();
                    var aNumber = aNumberObj[i].ToString();
                    var pDoc = pDocObj[i].ToString();
                    aNumber = aNumber.Replace("-", "");

                    // Populate data for Schema population
                    urlField = $"https://www.sec.gov/Archives/edgar/data/{cik}/{aNumber}/{pDoc}";

                    // Insert Azure Table bookkeeping to know which documents are available and which documents I've gathered
                    // Want to log all data available, and only update data that I have gathered
                    try
                    {
                        _logger?.LogTrace($"Checking if exists: CompanyName={companyName}, Form={form}, FilingDate={theDate}");
                        await InsertItemIfNotExists(companyName, form, theDate, urlField, connectionId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error inserting entity: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing filing: {ex.Message}");
                }
            }

            _logger.LogTrace($"beginning unprocessed data for company: {companyName}");
            var unprocessedData = await QueryUnprocessedData(connectionId).ConfigureAwait(false);
            foreach (var entity in unprocessedData)
            {
                try
                {
                    string entityCompanyName = entity.GetString("CompanyName");
                    
                    // Skip documents that don't belong to the current company being processed
                    if (!string.Equals(entityCompanyName, companyName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogTrace($"Skipping document for different company: {entityCompanyName} (processing: {companyName})");
                        continue;
                    }
                    
                    // Use the entity company name for the rest of the processing
                    string documentCompanyName = entityCompanyName;
                    var form = entity.GetString("Form");
                    DateTime? filingDate = DateTime.Parse(entity.GetString("FilingDate"));
                    var url = entity.GetString("Url");

                    // Generate reproducible itemId based on URL for consistency
                    itemId = DocumentIdGenerator.GenerateDocumentId(url);
                    
                    // Check if the form is one of the configured types
                    var includedFormTypes = await DataCollectionConfigurationService.GetIncludedFormTypesAsync();
                    if (includedFormTypes.Any(formType => form.ToUpper().Contains(formType.ToUpper())))
                    {
                        //itemId = $"{documentCompanyName}_Form{form}_{filingDate.Value.ToShortDateString()}".Replace("/", "_").Replace(" ", "_").Replace(".", "");
                        companyField = documentCompanyName;
                        titleField = $"{documentCompanyName} {form} {filingDate.Value.ToShortDateString().Replace("/", "-")}";
                        reportDateField = filingDate;
                        formField = form;
                        urlField = url;

                        retVal = await FetchWithExponentialBackoff(urlField).ConfigureAwait(false);
                        if(retVal == "FAILED")
                        {
                            _logger?.LogError($"Failed to fetch URL {urlField} after multiple retries.");
                            await UpdateProcessedItem(urlField, false, "Failed to fetch URL after multiple retries");
                            continue;
                        }
                        _logger?.LogTrace($"Fetched {urlField}");

                        string response = "";
                        
                        if(urlField.Contains(".pdf"))
                        {
                            _logger?.LogTrace($"PDF document found. Processing {urlField}");
                            try
                            {
                                // Fetch PDF content as bytes instead of string
                                byte[]? pdfBytes = await FetchBytesWithExponentialBackoff(urlField).ConfigureAwait(false);
                                
                                if (pdfBytes == null || pdfBytes.Length == 0)
                                {
                                    _logger?.LogError($"Failed to fetch PDF bytes from {urlField}");
                                    await UpdateProcessedItem(urlField, false, "Failed to fetch PDF content");
                                    continue;
                                }
                                
                                // Validate it's a real PDF
                                if (!PdfProcessingService.IsValidPdf(pdfBytes))
                                {
                                    _logger?.LogWarning($"Invalid PDF format for {urlField}");
                                    await UpdateProcessedItem(urlField, false, "Invalid PDF format");
                                    continue;
                                }
                                
                                // Extract text from PDF
                                response = await PdfProcessingService.ExtractTextFromPdfAsync(pdfBytes, 50); // Limit to 50 pages
                                
                                if (string.IsNullOrWhiteSpace(response))
                                {
                                    _logger?.LogWarning($"No text could be extracted from PDF {urlField}");
                                    await UpdateProcessedItem(urlField, false, "No extractable text in PDF");
                                    continue;
                                }
                                
                                _logger?.LogInformation($"Successfully extracted {response.Length} characters from PDF {urlField}");
                                
                                // Remove SEC filing header content from PDF text as well
                                response = RemoveSECHeader(response);
                            }
                            catch (Exception pdfEx)
                            {
                                _logger?.LogError($"Error processing PDF {urlField}: {pdfEx.Message}");
                                await UpdateProcessedItem(urlField, false, $"PDF processing error: {pdfEx.Message}");
                                continue;
                            }
                        }
                        else
                        {
                            // Process HTML documents as before
                            //OpenAIService openAIService = new OpenAIService();
                            //string response = openAIService.GetChatResponse(retVal);

                            HtmlDocument htmlDoc = new HtmlDocument();
                            htmlDoc.LoadHtml(retVal);
                            
                            // Extract text content and clean it up
                            response = htmlDoc.DocumentNode.InnerText;
                            
                            // Decode HTML entities (like &amp;, &lt;, &gt;, etc.)
                            response = System.Net.WebUtility.HtmlDecode(response);
                            
                            // Remove SEC filing header content before main document
                            response = RemoveSECHeader(response);
                            
                            // Remove XML declarations and processing instructions
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"<\?xml[^>]*\?>", "", RegexOptions.IgnoreCase);
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"<\?[^>]*\?>", "", RegexOptions.IgnoreCase);
                            
                            // Remove checkbox symbols and other special Unicode characters
                            response = response.Replace("☐", ""); // Empty checkbox
                            response = response.Replace("☑", ""); // Checked checkbox
                            response = response.Replace("☒", ""); // X-marked checkbox
                            response = response.Replace("✓", ""); // Checkmark
                            response = response.Replace("✗", ""); // X mark
                            
                            // Remove XBRL namespace declarations and technical metadata
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"iso4217:\w+", "", RegexOptions.IgnoreCase);
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"xbrli:\w+", "", RegexOptions.IgnoreCase);
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"\b\d{10}\b", ""); // Remove 10-digit numbers that look like IDs
                            
                            // Preserve semantic structure and clean up whitespace
                            response = PreserveSemanticStructure(response);
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"\s+", " "); // Replace multiple whitespace with single space
                            response = System.Text.RegularExpressions.Regex.Replace(response, @"[\r\n]+", "\n"); // Normalize line breaks
                            response = response.Trim(); // Remove leading/trailing whitespace
                        }


                        EdgarExternalItem edgarExternalItem = new EdgarExternalItem(itemId, titleField, companyField, urlField, reportDateField.Value.ToString("o"), formField, response);
                        
                        // Create the full ExternalItem for indexing and blob storage
                        ExternalItem fullExternalItem = CreateExternalItem(edgarExternalItem);

                        // Upload to blob storage if available
                        if (_blobServiceClient != null && !string.IsNullOrEmpty(processedBlobContainerName))
                        {
                            try
                            {
                                // Get a reference to the container
                                BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(processedBlobContainerName);
                                
                                // Create the container if it doesn't exist
                                await containerClient.CreateIfNotExistsAsync();
                                
                                // Upload raw HTML content
                                BlobClient blobClient = containerClient.GetBlobClient("/raw/" + itemId + ".html");
                                await blobClient.UploadAsync(new BinaryData(retVal), true).ConfigureAwait(false);
                                _logger?.LogTrace($"Uploaded raw HTML {itemId}.html to blob storage.");

                                // Upload the full ExternalItem as JSON in REST API format
                                blobClient = containerClient.GetBlobClient("/processed/" + itemId + ".json");
                                
                                // Convert ExternalItem to REST API JSON format
                                var jsonExternalItem = new ExternalItemForJson
                                {
                                    Acl = new List<AclForJson>
                                    {
                                        new AclForJson
                                        {
                                            Type = "everyone",
                                            Value = "Everyone", 
                                            AccessType = "grant"
                                        }
                                    },
                                    Properties = new Dictionary<string, object>
                                    {
                                        { "title", fullExternalItem.Properties?.AdditionalData?["Title"] ?? "" },
                                        { "company", fullExternalItem.Properties?.AdditionalData?["Company"] ?? "" },
                                        { "url", fullExternalItem.Properties?.AdditionalData?["Url"] ?? "" },
                                        { "dateFiled", fullExternalItem.Properties?.AdditionalData?["DateFiled"] ?? "" },
                                        { "form", fullExternalItem.Properties?.AdditionalData?["Form"] ?? "" },
                                        { "contentSummary", fullExternalItem.Properties?.AdditionalData?["ContentSummary"] ?? "" },
                                        { "keyFinancialMetrics", ConvertListToStringArray(fullExternalItem.Properties?.AdditionalData?["KeyFinancialMetrics"]) },
                                        { "businessSegments", ConvertListToStringArray(fullExternalItem.Properties?.AdditionalData?["BusinessSegments"]) },
                                        { "industryCategory", fullExternalItem.Properties?.AdditionalData?["IndustryCategory"] ?? "" },
                                        { "competitiveAdvantages", ConvertListToStringArray(fullExternalItem.Properties?.AdditionalData?["CompetitiveAdvantages"]) },
                                        { "esgContent", ConvertListToStringArray(fullExternalItem.Properties?.AdditionalData?["ESGContent"]) },
                                        { "uuid", fullExternalItem.Properties?.AdditionalData?["UUID"] ?? "" }
                                    },
                                    Content = new ContentForJson
                                    {
                                        Value = edgarExternalItem.contentField ?? "",
                                        Type = "text"
                                    }
                                };

                                string externalItemJson = System.Text.Json.JsonSerializer.Serialize(jsonExternalItem, new JsonSerializerOptions 
                                { 
                                    WriteIndented = true,
                                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                                });
                                await blobClient.UploadAsync(new BinaryData(externalItemJson), true).ConfigureAwait(false);
                                _logger?.LogTrace($"Uploaded REST API format ExternalItem JSON {itemId}.json to blob storage.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning($"Failed to upload to blob storage: {ex.Message}");
                            }
                        }
                        else
                        {
                            _logger?.LogTrace("Blob storage not configured. Skipping file uploads.");
                        }

                        // Load the ExternalItem to Graph
                        await ContentService.Load(fullExternalItem, connectionId);
                        
                        // Track that this company had a document successfully processed
                        _companiesWithProcessedDocuments.Add(documentCompanyName);
                        
                        // Mark document as successfully processed
                        await UpdateProcessedItem(urlField, true, null);
                        _logger?.LogTrace($"Successfully processed document: {urlField}");
                        
                        //externalItemData.Add(edgarExternalItem);
                    }
                }
                catch(Exception ex)
                {
                    _logger?.LogError($"Error processing unprocessed data: {ex.Message}");
                    // Mark document as failed if we have the URL
                    if (!string.IsNullOrEmpty(urlField))
                    {
                        await UpdateProcessedItem(urlField, false, $"Error processing document: {ex.Message}");
                    }
                }

            }
        }
        return externalItemData;
    }

    // Helper method to convert List<string> to safe format for JSON serialization
    private static object ConvertListToStringArray(object? listObj)
    {
        if (listObj is List<string> list && list.Count > 0)
        {
            // Clean each string in the list and remove duplicates
            var cleanedList = list
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().TrimEnd(',', ' ')) // Remove trailing commas and spaces
                .Where(s => s.Length > 3) // Only meaningful strings
                .Distinct(StringComparer.OrdinalIgnoreCase) // Remove duplicates (case insensitive)
                .Take(5) // Limit to avoid too much data
                .ToList();
            
            if (cleanedList.Count > 0)
            {
                // Instead of returning an array, return a concatenated string
                // This might be safer for Microsoft Graph deserialization
                return string.Join("; ", cleanedList);
            }
        }
        // Return null instead of empty array to avoid Microsoft Graph deserialization issues
        return null!;
    }

    // Content extraction methods for structured data
    public static string ExtractExecutiveSummary(string content)
    {
        try
        {
            // Look for common summary sections in SEC filings
            var summaryPatterns = new[]
            {
                @"(?i)(?:business\s+overview|executive\s+summary|overview\s+of\s+operations|business\s+description)(.*?)(?=\n\s*(?:item|table|risk|competition)|\Z)",
                @"(?i)(?:item\s+1\s*[.\-–]?\s*business)(.*?)(?=item\s+[12][ab]?|risk\s+factors|\Z)",
                @"(?i)(?:forward\s*-?\s*looking\s+statements?)(.*?)(?=item|table|\Z)"
            };

            foreach (var pattern in summaryPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, pattern, RegexOptions.Singleline);
                if (match.Success && match.Groups[1].Value.Length > 100)
                {
                    var summary = match.Groups[1].Value.Trim();
                    // Clean up and limit length
                    summary = System.Text.RegularExpressions.Regex.Replace(summary, @"\s+", " ");
                    return summary.Length > 500 ? summary.Substring(0, 500) + "..." : summary;
                }
            }

            // Fallback: Take first few meaningful paragraphs
            var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var meaningfulParagraphs = paragraphs
                .Where(p => p.Length > 50 && !p.ToUpper().Contains("TABLE OF CONTENTS"))
                .Take(3)
                .ToList();

            if (meaningfulParagraphs.Any())
            {
                var summary = string.Join(" ", meaningfulParagraphs);
                return summary.Length > 500 ? summary.Substring(0, 500) + "..." : summary;
            }

            return "Summary not available";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error extracting executive summary: {ex.Message}");
            return "Summary extraction failed";
        }
    }

    public static List<string> ExtractFinancialMetrics(string content)
    {
        try
        {
            var financialTerms = new List<string>();
            
            // Patterns for common financial metrics
            var patterns = new Dictionary<string, string>
            {
                ["Revenue/Sales"] = @"(?i)(?:total\s+)?(?:revenue|sales|net\s+sales)[:\s]*\$?[\d,]+(?:\.\d+)?(?:\s*(?:million|billion|thousand))?",
                ["Net Income"] = @"(?i)(?:net\s+income|net\s+earnings|profit)[:\s]*\$?[\d,]+(?:\.\d+)?(?:\s*(?:million|billion|thousand))?",
                ["Assets"] = @"(?i)(?:total\s+)?assets[:\s]*\$?[\d,]+(?:\.\d+)?(?:\s*(?:million|billion|thousand))?",
                ["Debt"] = @"(?i)(?:total\s+)?(?:debt|liabilities)[:\s]*\$?[\d,]+(?:\.\d+)?(?:\s*(?:million|billion|thousand))?",
                ["Earnings Per Share"] = @"(?i)(?:earnings\s+per\s+share|eps)[:\s]*\$?[\d,]+\.\d+",
                ["Market Cap"] = @"(?i)market\s+cap(?:italization)?[:\s]*\$?[\d,]+(?:\.\d+)?(?:\s*(?:million|billion|thousand))?",
                ["Growth Rate"] = @"(?i)(?:growth|increase|decrease)[^.]*?[\d,]+\.?\d*%"
            };

            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern.Value);
                foreach (Match match in matches.Take(3)) // Limit to avoid too many results
                {
                    if (match.Success && match.Value.Length < 100)
                    {
                        // Clean the match value by removing HTML tags, extra whitespace, and special characters
                        var cleanMatch = System.Text.RegularExpressions.Regex.Replace(match.Value, @"<[^>]*>", ""); // Remove HTML tags
                        cleanMatch = System.Text.RegularExpressions.Regex.Replace(cleanMatch, @"\s+", " ").Trim(); // Normalize whitespace
                        cleanMatch = System.Text.RegularExpressions.Regex.Replace(cleanMatch, @"[^\w\s\$\.\,%\-:]", ""); // Remove special characters except common financial ones
                        cleanMatch = cleanMatch.TrimEnd(',', ' '); // Remove trailing commas and spaces
                        
                        if (!string.IsNullOrEmpty(cleanMatch) && cleanMatch.Length > 5)
                        {
                            // Create a more standardized format
                            var formattedMetric = $"{pattern.Key}: {cleanMatch}";
                            // Avoid duplicates by checking if similar entry already exists
                            if (!financialTerms.Any(t => t.StartsWith($"{pattern.Key}:") && 
                                System.Text.RegularExpressions.Regex.Replace(t.ToLower(), @"[^\w\s]", "") == 
                                System.Text.RegularExpressions.Regex.Replace(formattedMetric.ToLower(), @"[^\w\s]", "")))
                            {
                                financialTerms.Add(formattedMetric);
                            }
                        }
                    }
                }
            }

            return financialTerms.Take(10).ToList(); // Limit to top 10 metrics
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error extracting financial metrics: {ex.Message}");
            return new List<string> { "Financial metrics extraction failed" };
        }
    }

    public static List<string> ExtractBusinessSegments(string content)
    {
        try
        {
            var segments = new HashSet<string>(); // Use HashSet to avoid duplicates
            
            // Look for business segment patterns
            var segmentPatterns = new[]
            {
                @"(?i)(?:business\s+segment|operating\s+segment|reportable\s+segment)[s]?[:\s]*([^.\n]{10,100})",
                @"(?i)(?:our\s+)?(?:primary\s+)?(?:business|operations?)\s+(?:include|consist|focus)[^.]{10,150}",
                @"(?i)(?:we\s+operate|operates?)\s+(?:in|through)[^.]{10,100}",
                @"(?i)(?:main\s+)?(?:product|service)\s+(?:line|offering)[s]?[:\s]*([^.\n]{10,100})"
            };

            foreach (var pattern in segmentPatterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);
                foreach (Match match in matches.Take(5))
                {
                    if (match.Success)
                    {
                        var segment = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                        
                        // Clean the segment text
                        segment = System.Text.RegularExpressions.Regex.Replace(segment, @"<[^>]*>", ""); // Remove HTML tags
                        segment = System.Text.RegularExpressions.Regex.Replace(segment, @"\s+", " ").Trim(); // Normalize whitespace
                        segment = System.Text.RegularExpressions.Regex.Replace(segment, @"[^\w\s\-&,.]", ""); // Remove special characters except common business ones
                        
                        if (segment.Length > 15 && segment.Length < 150 && !string.IsNullOrEmpty(segment))
                        {
                            segments.Add(segment);
                        }
                    }
                }
            }

            // Look for industry-specific keywords
            var industryKeywords = new[]
            {
                "technology", "healthcare", "financial services", "retail", "manufacturing",
                "energy", "telecommunications", "aerospace", "pharmaceuticals", "automotive",
                "real estate", "media", "transportation", "utilities", "consumer goods"
            };

            foreach (var keyword in industryKeywords)
            {
                if (content.ToLower().Contains(keyword))
                {
                    var pattern = $@"(?i){keyword}[^.\n]{{10,100}}";
                    var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);
                    foreach (Match match in matches.Take(2))
                    {
                        if (match.Success && match.Value.Length < 150)
                        {
                            var cleanMatch = System.Text.RegularExpressions.Regex.Replace(match.Value, @"\s+", " ").Trim();
                            segments.Add(cleanMatch);
                        }
                    }
                }
            }

            return segments.Take(8).ToList(); // Limit to top 8 segments
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error extracting business segments: {ex.Message}");
            return new List<string> { "Business segments extraction failed" };
        }
    }

    // Method to create a full ExternalItem from EdgarExternalItem
    public static ExternalItem CreateExternalItem(EdgarExternalItem item)
    {
        try
        {
            var externalItem = new ExternalItem
            {
                Id = itemId,
                Properties = new()
                {
                    AdditionalData = new Dictionary<string, object> {
                        { "Title", item.titleField ?? "" },
                        { "Company", item.companyField ?? "" },
                        { "Url", item.urlField ?? "" },
                        { "IconUrl", "https://www.sec.gov/themes/investor_gov/images/sec-logo.png" },
                        { "DateFiled", item.reportDateField ?? "" },
                        { "Form", item.formField ?? "Unknown" },
                        { "ContentSummary", ExtractExecutiveSummary(item.contentField ?? "") },
                        { "KeyFinancialMetrics", ConvertListToStringArray(ExtractFinancialMetrics(item.contentField ?? "")) },
                        { "BusinessSegments", ConvertListToStringArray(ExtractBusinessSegments(item.contentField ?? "")) },
                        { "IndustryCategory", DetermineIndustryCategory(item.companyField ?? "") },
                        { "CompetitiveAdvantages", ConvertListToStringArray(ContentEnhancementService.ExtractCompetitiveAdvantages(item.contentField ?? "")) },
                        { "ESGContent", ConvertListToStringArray(ContentEnhancementService.ExtractESGContent(item.contentField ?? "")) },
                        { "UUID", itemId ?? "" }
                    }
                },
                Content = new()
                {
                    Value = ContentService.EnhanceContentForCopilot(item.contentField ?? "", item.companyField ?? "", item.formField ?? ""),
                    Type = ExternalItemContentType.Html
                },
                Acl = new()
                {
                    new()
                    {
                        Type = AclType.Everyone,
                        Value = "Everyone",
                        AccessType = AccessType.Grant
                    }
                }
            };

            return externalItem;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error creating ExternalItem: {ex.Message}");
            throw;
        }
    }

    // Method to remove SEC filing header content before main document
    public static string RemoveSECHeader(string content)
    {
        try
        {
            // Look for the main content marker patterns - ordered by specificity
            var contentStartPatterns = new[]
            {
                @"UNITED\s+STATES\s+SECURITIES\s+AND\s+EXCHANGE\s+COMMISSION",
                @"UNITED\s*STATES\s*SECURITIES\s*AND\s*EXCHANGE",
                @"UNITED\s*STATES\s*SECURITIES",
                @"UNITED\s*STATESSECURITIES",
                @"STATESSECURITIES", // Handle cases without UNITED prefix
                @"STATES\s+SECURITIES", // Handle cases with space but without UNITED
                @"SECURITIES\s+AND\s+EXCHANGE\s+COMMISSION",
                // Alternative patterns that might appear
                @"Form\s+\d+-[KQ]\s+.*?SECURITIES",
                @"ANNUAL\s+REPORT\s+PURSUANT\s+TO\s+SECTION",
                @"QUARTERLY\s+REPORT\s+PURSUANT\s+TO\s+SECTION"
            };

            foreach (var pattern in contentStartPatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    // Find the start of the main content and keep everything from there
                    int startIndex = match.Index;
                    string mainContent = content.Substring(startIndex);
                    
                    _logger?.LogTrace($"Removed SEC header content using pattern '{pattern}' (matched: '{match.Value}'). Original length: {content.Length}, New length: {mainContent.Length}, Removed: {content.Length - mainContent.Length} characters");
                    return mainContent;
                }
            }

            // If no pattern found, check if the content is very short and might be all header
            if (content.Length < 500)
            {
                _logger?.LogTrace($"Content is short ({content.Length} chars), no SEC header pattern found, keeping original content");
            }
            else
            {
                _logger?.LogTrace($"No SEC header pattern found in content of {content.Length} characters, keeping original content");
            }
            
            return content;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error removing SEC header: {ex.Message}");
            return content; // Return original content if header removal fails
        }
    }

    // Content enhancement methods for better Copilot understanding
    public static string PreserveSemanticStructure(string content)
    {
        try
        {
            // Preserve SEC filing structure by marking important sections
            content = System.Text.RegularExpressions.Regex.Replace(content, 
                @"(?i)(ITEM\s+\d+[A-Z]?\.?\s*[–\-]?\s*.*?)(?=ITEM\s+\d+|SIGNATURE|\Z)", 
                "<section data-type=\"filing-item\">$1</section>", 
                RegexOptions.Multiline);

            // Mark financial data with semantic tags
            content = System.Text.RegularExpressions.Regex.Replace(content, 
                @"(\$[\d,]+(?:\.\d+)?(?:\s*(?:million|billion|thousand))?)", 
                "<financial data-type=\"currency\">$1</financial>");

            // Mark percentages as financial metrics
            content = System.Text.RegularExpressions.Regex.Replace(content, 
                @"(\d+\.?\d*%)", 
                "<financial data-type=\"percentage\">$1</financial>");

            // Mark years as temporal data
            content = System.Text.RegularExpressions.Regex.Replace(content, 
                @"\b(19\d{2}|20[0-2]\d)\b", 
                "<temporal data-type=\"year\">$1</temporal>");

            // Mark table headers and important business terms
            var businessTerms = new[] { "revenue", "income", "assets", "liabilities", "equity", "cash flow", "earnings", "profit", "loss" };
            foreach (var term in businessTerms)
            {
                content = System.Text.RegularExpressions.Regex.Replace(content,
                    $@"(?i)\b({term}[s]?)\b",
                    $"<business-term data-type=\"{term.ToLower()}\">$1</business-term>");
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error preserving semantic structure: {ex.Message}");
            return content; // Return original content if enhancement fails
        }
    }

    public static string DetermineIndustryCategory(string companyName)
    {
        try
        {
            var industryKeywords = new Dictionary<string, string[]>
            {
                ["Technology"] = new[] { "tech", "software", "microsoft", "apple", "google", "amazon", "meta", "tesla", "intel", "nvidia" },
                ["Healthcare"] = new[] { "health", "pharma", "medical", "biotech", "hospital", "johnson", "pfizer", "merck" },
                ["Financial"] = new[] { "bank", "financial", "insurance", "capital", "goldman", "morgan", "wells fargo", "jpmorgan" },
                ["Energy"] = new[] { "oil", "gas", "energy", "exxon", "chevron", "bp", "shell", "renewable" },
                ["Retail"] = new[] { "retail", "store", "walmart", "target", "costco", "home depot", "consumer" },
                ["Manufacturing"] = new[] { "manufacturing", "industrial", "boeing", "caterpillar", "general electric", "3m" },
                ["Telecommunications"] = new[] { "telecom", "wireless", "verizon", "att", "t-mobile", "comcast" },
                ["Automotive"] = new[] { "auto", "ford", "general motors", "toyota", "volkswagen", "automotive" }
            };

            var lowerCompanyName = companyName.ToLower();
            
            foreach (var industry in industryKeywords)
            {
                if (industry.Value.Any(keyword => lowerCompanyName.Contains(keyword)))
                {
                    return industry.Key;
                }
            }

            return "General";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error determining industry category: {ex.Message}");
            return "Unknown";
        }
    }

    // Define a method to insert an item if it does not exist in the table
    public static async Task InsertItemIfNotExists(string companyName, string form, DateTime filingDate, string url, string? connectionId = null)
    {
        // Check if the form is one of the configured types
        var includedFormTypes = await DataCollectionConfigurationService.GetIncludedFormTypesAsync();
        if (!includedFormTypes.Any(formType => form.ToUpper().Contains(formType.ToUpper())))
        {
            _logger?.LogTrace($"Skipping form not in configured types: {form}");
            return;
        }

        try
        {
            // Use new storage service if available, otherwise fall back to old Azure Table Storage
            if (_storageService != null)
            {
                await _storageService.TrackDocumentAsync(companyName, form, filingDate, url, connectionId);
                _logger?.LogTrace($"Tracked document via storage service: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");
            }
            else
            {
                // Legacy Azure Table Storage fallback
                // Skip if Azure Table Storage is not available
                if (_tableClient == null)
                {
                    _logger?.LogTrace("No storage service available. Skipping entity insertion.");
                    return;
                }

                _logger?.LogTrace($"Checking if exists: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");

                // Define the query filter
                string filter = $"Url eq '{url}'";

                // Execute the query
                List<TableEntity> results = _tableClient.Query<TableEntity>(filter).ToList();

                if (results.Count == 0)
                {
                    // Create a new entity
                    var newEntity = new TableEntity
                    {
                        PartitionKey = companyName,
                        RowKey = Guid.NewGuid().ToString(),
                        ["CompanyName"] = companyName,
                        ["Form"] = form,
                        ["FilingDate"] = filingDate.ToShortDateString(),
                        ["Url"] = url,
                        ["Processed"] = false
                    };

                    // Insert the new entity
                    await _tableClient.AddEntityAsync(newEntity).ConfigureAwait(false);
                    _logger?.LogTrace($"Inserted new entity: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");
                }
                else
                {
                    _logger?.LogTrace($"Entity already exists: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error tracking document: {ex.Message}");
        }
    }

    // Define a method to update the processed item in the table
    public static async Task UpdateProcessedItem(string url, bool success = true, string? errorMessage = null)
    {
        try
        {
            // Use new storage service if available, otherwise fall back to old Azure Table Storage
            if (_storageService != null)
            {
                await _storageService.MarkProcessedAsync(url, success, errorMessage);
                _logger?.LogTrace($"Marked document as processed via storage service: Url={url}, Success={success}");
            }
            else
            {
                // Legacy Azure Table Storage fallback
                // Skip if Azure Table Storage is not available
                if (_tableClient == null)
                {
                    _logger?.LogTrace("No storage service available. Skipping processed item update.");
                    return;
                }

                // Define the query filter
                string filter = $"Url eq '{url}'";
                // Execute the query
                List<TableEntity> results = _tableClient.Query<TableEntity>(filter).ToList();
                if (results.Count > 0)
                {
                    _logger?.LogTrace($"Found entity to update: Url={url}");
                    // Update the "Processed" property of the first entity found
                    var entityToUpdate = results[0];
                    entityToUpdate["Processed"] = true;
                    // Update the entity in the table
                    await _tableClient.UpdateEntityAsync(entityToUpdate, Azure.ETag.All).ConfigureAwait(false);
                    _logger?.LogTrace($"Updated entity: Url={url}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error updating processed item: {ex.Message}");
        }
    }

    // Define a method to query unprocessed data from the table
    public static async Task<List<TableEntity>> QueryUnprocessedData(string? connectionId = null)
    {
        try
        {
            // Use new storage service if available
            if (_storageService != null)
            {
                var unprocessedDocuments = await _storageService.GetUnprocessedAsync(connectionId);
                _logger?.LogTrace($"Found {unprocessedDocuments.Count} unprocessed documents via storage service for connection {connectionId ?? "default"}");
                
                // Convert to TableEntity format for compatibility with existing code
                return unprocessedDocuments.Select(doc => new TableEntity
                {
                    PartitionKey = doc.CompanyName,
                    RowKey = doc.Id,
                    ["CompanyName"] = doc.CompanyName,
                    ["Form"] = doc.Form,
                    ["FilingDate"] = doc.FilingDate.ToString(),
                    ["Url"] = doc.Url,
                    ["Processed"] = doc.Processed
                }).ToList();
            }
            else
            {
                // Legacy Azure Table Storage fallback
                if (_tableClient == null)
                {
                    _logger?.LogTrace("No storage service available. Returning empty list.");
                    return new List<TableEntity>();
                }

                // Define the query filter - for legacy storage, we can't filter by connection
                // so we'll return all unprocessed and let calling code filter if needed
                string filter = "Processed eq false";
                // Execute the query
                var results = await Task.Run(() => _tableClient.Query<TableEntity>(filter).ToList());
                _logger?.LogTrace($"Found {results.Count} unprocessed documents via Azure Table Storage (legacy - no connection filtering)");
                return results;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error querying unprocessed data: {ex.Message}");
            return new List<TableEntity>();
        }
    }

    // Define a method to fetch data with exponential backoff
    private static async Task<string> FetchWithExponentialBackoff(string url)
    {
        int maxRetries = 5;
        int delay = 5000; // Initial delay in milliseconds

        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                var response = await _client.GetAsync(url).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {

                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        string retryAfter = values.First();
                        _logger.LogTrace($"Rate limit exceeded. Retrying after {retryAfter} seconds.");
                        int retryAfterSeconds = int.Parse(retryAfter);
                        await Task.Delay(retryAfterSeconds * 1000);
                    }
                    else
                    {
                        _logger.LogWarning($"HTTP 429 Too Many Requests. Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        delay *= 2; // Exponential backoff
                    }
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching URL {url}: {ex.Message}");
                if (retry == maxRetries )
                {
                    // Write to storage table
                    _logger.LogTrace($"Max retries reached for URL {url}. Giving up.");
                }
                await Task.Delay(delay).ConfigureAwait(false);
                delay *= 2; // Exponential backoff
            }
        }

        return "FAILED";
    }

    // Define a method to fetch binary data (for PDFs) with exponential backoff
    private static async Task<byte[]?> FetchBytesWithExponentialBackoff(string url)
    {
        int maxRetries = 5;
        int delay = 5000; // Initial delay in milliseconds

        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                var response = await _client.GetAsync(url).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        string retryAfter = values.First();
                        _logger?.LogTrace($"Rate limit exceeded. Retrying after {retryAfter} seconds.");
                        int retryAfterSeconds = int.Parse(retryAfter);
                        await Task.Delay(retryAfterSeconds * 1000);
                    }
                    else
                    {
                        _logger?.LogWarning($"HTTP 429 Too Many Requests. Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        delay *= 2; // Exponential backoff
                    }
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error fetching binary data from URL {url}: {ex.Message}");
                if (retry == maxRetries)
                {
                    _logger?.LogTrace($"Max retries reached for URL {url}. Giving up.");
                }
                await Task.Delay(delay).ConfigureAwait(false);
                delay *= 2; // Exponential backoff
            }
        }

        return null;
    }

    // Define a method to initialize the logger
    public static void InitializeLogger(ILogger logger)
    {
        _logger = logger;
    }

    // Initialize the storage service for document tracking
    public static async Task InitializeStorageServiceAsync(ICrawlStorageService storageService)
    {
        _storageService = storageService;
        await _storageService.InitializeAsync();
        _logger?.LogInformation("EdgarService storage service initialized: {StorageType}", _storageService.GetStorageType());
    }
}

// Custom classes for REST API JSON serialization
public class ExternalItemForJson
{
    [System.Text.Json.Serialization.JsonPropertyName("acl")]
    public List<AclForJson> Acl { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public ContentForJson Content { get; set; } = new();
}

public class AclForJson
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("accessType")]
    public string AccessType { get; set; } = "";
}

public class ContentForJson
{
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "";
}
