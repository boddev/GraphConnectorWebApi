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

namespace ApiGraphActivator.Services;

public static class EdgarService
{
    // Define static members for HttpClient, LoggingService, TableClient, and various configuration strings
    private static readonly HttpClient _client;
    private static ILogger? _logger;
    private static TableClient? _tableClient;
    private static BlobServiceClient? _blobServiceClient;
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

    // Define an asynchronous static method to hydrate lookup data for specific companies
    async public static Task<List<EdgarExternalItem>> HydrateLookupDataForCompanies(List<Company> companies)
    {
        _logger?.LogInformation("Processing {CompanyCount} companies", companies.Count);
        
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
                var companyFilingDocuments = await GetDocument(filingJson).ConfigureAwait(false);
                
                if (companyFilingDocuments != null)
                {
                    filingDocuments.AddRange(companyFilingDocuments);
                }
                
                _logger?.LogTrace($"Processed {companyFilingDocuments?.Count ?? 0} documents for {company.Ticker}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error processing companies: {ex.Message}");
            return filingDocuments;
        }

        _logger?.LogInformation("Completed processing. Total documents: {DocumentCount}", filingDocuments.Count);
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
    async public static Task<List<EdgarExternalItem>> GetDocument(string filingString)
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
                    int yearsOfData = 0;
                    Int32.TryParse(Environment.GetEnvironmentVariable("YearsOfData"), out yearsOfData);

                    if (yearsOfData == 0)
                    {
                        yearsOfData = -3;
                    }

                    // Check if the date is more than 2 years in the past
                    if (theDate < DateTime.Now.AddYears(yearsOfData))
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
                        await InsertItemIfNotExists(companyName, form, theDate, urlField).ConfigureAwait(false);
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

            _logger.LogTrace($"beginning unprocessed data");
            var unprocessedData = await QueryUnprocessedData().ConfigureAwait(false);
            foreach (var entity in unprocessedData)
            {
                try
                {
                    string companyName = entity.GetString("CompanyName");
                    var form = entity.GetString("Form");
                    DateTime? filingDate = DateTime.Parse(entity.GetString("FilingDate"));
                    var url = entity.GetString("Url");

                        // Regex to match non-Base64 characters
                    itemId = $"{companyName}_Form{form}_{filingDate.Value.ToShortDateString()}";
                    string pattern = @"[^A-Za-z0-9+/=]";
                    itemId = Regex.Replace(itemId, pattern, "_");
                    itemId = itemId.Replace("/","_");
                    // Check if the form is one of the specified types
                    if (form.ToUpper().Contains("10-K") || form.ToUpper().Contains("10-Q") || form.ToUpper().Contains("8-K") || form.ToUpper().Contains("DEF 14A"))
                    {
                        //itemId = $"{companyName}_Form{form}_{filingDate.Value.ToShortDateString()}".Replace("/", "_").Replace(" ", "_").Replace(".", "");
                        companyField = companyName;
                        titleField = $"{companyName} {form} {filingDate.Value.ToShortDateString().Replace("/", "-")}";
                        reportDateField = filingDate;
                        formField = form;
                        urlField = url;

                        retVal = await FetchWithExponentialBackoff(urlField).ConfigureAwait(false);
                        if(retVal == "FAILED")
                        {
                            _logger.LogError($"Failed to fetch URL {urlField} after multiple retries.");
                            continue;
                        }
                        _logger.LogTrace($"Fetched {urlField}");

                        if(urlField.Contains(".pdf"))
                        {
                            _logger.LogTrace($"PDF document found. Skipping {urlField}.");
                            continue;
                        }

                        //OpenAIService openAIService = new OpenAIService();
                        //string response = openAIService.GetChatResponse(retVal);

                        HtmlDocument htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(retVal);
                        string response = htmlDoc.DocumentNode.InnerText;
                        // Remove lines that start with "gaap:"
                        // string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        // string cleanedResponse = string.Join(Environment.NewLine, 
                        //     lines.Where(line => !line.TrimStart().StartsWith("gaap:", StringComparison.OrdinalIgnoreCase)));

                        // // Use the cleaned response instead
                        // response = cleanedResponse;

                        // Upload to blob storage if available
                        if (_blobServiceClient != null && !string.IsNullOrEmpty(processedBlobContainerName))
                        {
                            try
                            {
                                // Get a reference to the container
                                BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(processedBlobContainerName);
                                
                                // Create the container if it doesn't exist
                                await containerClient.CreateIfNotExistsAsync();
                                
                                // Get a reference to the blob
                                BlobClient blobClient = containerClient.GetBlobClient("/raw/" + itemId + ".html");
                                await blobClient.UploadAsync(new BinaryData(retVal), true).ConfigureAwait(false);
                                _logger?.LogTrace($"Uploaded HTML {itemId}.html to blob storage.");

                                blobClient = containerClient.GetBlobClient("/openai/" + itemId + ".txt");
                                await blobClient.UploadAsync(new BinaryData(response), true).ConfigureAwait(false);
                                _logger?.LogTrace($"Uploaded OpenAI: {itemId}.text to blob storage.");
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

                        EdgarExternalItem edgarExternalItem = new EdgarExternalItem(itemId, titleField, companyField, urlField, reportDateField.Value.ToString("o"), formField, response);
                        ContentService.Transform(edgarExternalItem);
                        //externalItemData.Add(edgarExternalItem);
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError($"Error processing unprocessed data: {ex.Message}");
                }

            }
        }
        return externalItemData;
    }

    // Define a method to insert an item if it does not exist in the table
    public static async Task InsertItemIfNotExists(string companyName, string form, DateTime filingDate, string url)
    {
        // Skip if Azure Table Storage is not available
        if (_tableClient == null)
        {
            _logger?.LogTrace("Azure Table Storage not available. Skipping entity insertion.");
            return;
        }

        if (form.ToUpper().Contains("10-K") || form.ToUpper().Contains("10-Q") || form.ToUpper().Contains("8-K") || form.ToUpper().Contains("DEF 14A"))
        {
            _logger?.LogTrace($"Checking if exists: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");
        }
        else
        {
            _logger?.LogTrace($"Skipping non-10-K/10-Q/8-K/DEF 14A form: {form}");
            return;
        }

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

            try
            {
                // Insert the new entity
                await _tableClient.AddEntityAsync(newEntity).ConfigureAwait(false);
                _logger?.LogTrace($"Inserted new entity: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error inserting entity: {ex.Message}");
            }
        }
        else
        {
            _logger?.LogTrace($"Entity already exists: CompanyName={companyName}, Form={form}, FilingDate={filingDate}");
        }
    }

    // Define a method to update the processed item in the table
    public static async Task UpdateProcessedItem(string url)
    {
        // Skip if Azure Table Storage is not available
        if (_tableClient == null)
        {
            _logger?.LogTrace("Azure Table Storage not available. Skipping processed item update.");
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

    // Define a method to query unprocessed data from the table
    public static async Task<List<TableEntity>> QueryUnprocessedData()
    {
        // Return empty list if Azure Table Storage is not available
        if (_tableClient == null)
        {
            _logger?.LogTrace("Azure Table Storage not available. Returning empty unprocessed data list.");
            return new List<TableEntity>();
        }

        // Define the query filter
        string filter = $"Processed eq false";

        // Execute the query
        List<TableEntity> results = _tableClient.Query<TableEntity>(filter).ToList();
        return results;
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

    // Define a method to initialize the logger
    public static void InitializeLogger(ILogger logger)
    {
        _logger = logger;
    }
}
