using System;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Microsoft.VisualBasic;

namespace ApiGraphActivator.Services;

public static class EdgarService
{
    // Define static members for HttpClient, LoggingService, TableClient, and various configuration strings
    private static readonly HttpClient _client;
    private static ILogger? _logger;
    private static TableClient? _tableClient;
    static string? connectionString;
    static string? companyName;
    static string? companySymbol;
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
        _client.DefaultRequestHeaders.Add("User-Agent", $"Microsoft/1.0 ({Environment.GetEnvironmentVariable("EmailAddress")})");
        _logger?.LogTrace("HttpClient initialized with User-Agent header.");

        connectionString = Environment.GetEnvironmentVariable("TableStorage");
        companyTableName = Environment.GetEnvironmentVariable("CompanyTableName");
        processedTableName = Environment.GetEnvironmentVariable("ProcessedTableName");
        _tableClient = new TableClient(connectionString, processedTableName);
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
        List<TableEntity> tableEntities;
        List<EdgarExternalItem> filingDocuments = new List<EdgarExternalItem>();

        try
        {
            TableClient tc = new TableClient(connectionString, companyTableName);
            // Retrieve all entities from the table
            tableEntities = tc.Query<TableEntity>().ToList();
            // Process the table entities as needed
            foreach (var entity in tableEntities)
            {
                _logger.LogTrace($"PartitionKey: {entity.PartitionKey}, RowKey: {entity.RowKey}");
                companyName = entity.GetString("RowKey").Trim();
                companySymbol = entity.GetString("Symbol").Trim();
                cikLookup = ExtractCIK(company_tkr_response, companySymbol);

                if (cikLookup == "Company not found")
                {
                    _logger.LogError($"Company not found for {companySymbol}");
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
            _logger.LogError($"Error retrieving entities from table: {ex.Message}");
            return filingDocuments;
        }

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

                    // Check if the form is one of the specified types
                    //if (form.ToUpper().Contains("10-K") || form.ToUpper().Contains("10-Q") || form.ToUpper().Contains("8-K"))
                    {
                        itemId = $"{companyName}_Form{form}_{filingDate.Value.ToShortDateString()}".Replace("/", "_").Replace(" ", "_").Replace(".", "");
                        companyField = companyName;
                        titleField = $"{companyName} {form} {reportDate}";
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

                        EdgarExternalItem edgarExternalItem = new EdgarExternalItem(itemId, titleField, companyField, urlField, reportDateField.Value.ToString("o"), formField, retVal);
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
