using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApiGraphActivator.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP tool for document summarization using AI
/// </summary>
public class DocumentSummarizationTool : McpToolBase
{
    private readonly OpenAIService _openAIService;
    private readonly ILogger<DocumentSummarizationTool> _logger;
    private readonly HttpClient _httpClient;

    public DocumentSummarizationTool(OpenAIService openAIService, ILogger<DocumentSummarizationTool> logger, HttpClient httpClient)
    {
        _openAIService = openAIService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public override string Name => "summarize_document";

    public override string Description => 
        "Generate comprehensive summaries of documents using AI. Supports PDFs and text content with customizable summary length and focus areas.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentUrl = new
            {
                type = "string",
                description = "URL of the document to summarize (PDF supported)"
            },
            documentContent = new
            {
                type = "string",
                description = "Direct text content to summarize (alternative to documentUrl)"
            },
            summaryLength = new
            {
                type = "integer",
                minimum = 1,
                maximum = 5,
                description = "Summary length (1=brief, 2=short, 3=medium, 4=detailed, 5=comprehensive)"
            },
            focusAreas = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional focus areas for the summary (e.g., 'financial performance', 'risk factors')"
            },
            includeKeyMetrics = new
            {
                type = "boolean",
                description = "Whether to include key metrics in the summary"
            },
            maxPages = new
            {
                type = "integer",
                minimum = 1,
                maximum = 500,
                description = "Maximum number of pages to process for PDF documents"
            }
        }
    };

    public async Task<McpToolResponse<DocumentSummarizationResult>> ExecuteAsync(DocumentSummarizationParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting document summarization");
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<DocumentSummarizationResult>.Error($"Validation failed: {errors}");
            }

            if (string.IsNullOrEmpty(parameters.DocumentUrl) && string.IsNullOrEmpty(parameters.DocumentContent))
            {
                return McpToolResponse<DocumentSummarizationResult>.Error("Either documentUrl or documentContent must be provided");
            }

            string documentText;
            var documentInfo = new DocumentInfo();

            // Extract text from document
            if (!string.IsNullOrEmpty(parameters.DocumentUrl))
            {
                if (PdfProcessingService.IsPdfUrl(parameters.DocumentUrl))
                {
                    _logger.LogInformation("Processing PDF from URL: {Url}", parameters.DocumentUrl);
                    documentText = await PdfProcessingService.ExtractTextFromPdfUrlAsync(parameters.DocumentUrl, _httpClient, parameters.MaxPages);
                    documentInfo.Source = parameters.DocumentUrl;
                    documentInfo.DocumentType = "PDF";
                }
                else
                {
                    return McpToolResponse<DocumentSummarizationResult>.Error("Only PDF URLs are currently supported");
                }
            }
            else
            {
                documentText = parameters.DocumentContent!;
                documentInfo.Source = "Direct content";
                documentInfo.DocumentType = "Text";
            }

            if (string.IsNullOrWhiteSpace(documentText))
            {
                return McpToolResponse<DocumentSummarizationResult>.Error("No text content could be extracted from the document");
            }

            // Update document info
            documentInfo.CharacterCount = documentText.Length;
            documentInfo.ProcessingDate = DateTime.UtcNow;

            // Generate summary using OpenAI
            var summaryPrompt = BuildSummaryPrompt(documentText, parameters);
            var aiResponse = _openAIService.GetChatResponse(summaryPrompt);

            // Parse AI response
            var result = ParseSummaryResponse(aiResponse, documentInfo);
            
            _logger.LogInformation("Document summarization completed successfully");
            return McpToolResponse<DocumentSummarizationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document summarization");
            return McpToolResponse<DocumentSummarizationResult>.Error($"Summarization failed: {ex.Message}");
        }
    }

    private string BuildSummaryPrompt(string documentText, DocumentSummarizationParameters parameters)
    {
        var lengthDescription = parameters.SummaryLength switch
        {
            1 => "very brief (1-2 sentences)",
            2 => "short (1-2 paragraphs)",
            3 => "medium (3-4 paragraphs)",
            4 => "detailed (5-7 paragraphs)",
            5 => "comprehensive (8+ paragraphs)",
            _ => "medium (3-4 paragraphs)"
        };

        var focusAreasText = parameters.FocusAreas?.Any() == true 
            ? $"Pay special attention to these areas: {string.Join(", ", parameters.FocusAreas)}." 
            : "";

        var metricsInstruction = parameters.IncludeKeyMetrics 
            ? "Also extract and list key financial metrics, dates, and numerical data as a separate section." 
            : "";

        return $"""
Please provide a {lengthDescription} summary of the following document. {focusAreasText} {metricsInstruction}

Format your response as follows:
SUMMARY:
[Your summary here]

KEY POINTS:
- [Key point 1]
- [Key point 2]
- [Key point 3]
[Add more as needed]

{(parameters.IncludeKeyMetrics ? """
KEY METRICS:
- [Metric name]: [Value]
- [Metric name]: [Value]
[Add more as needed]
""" : "")}

Document text:
{documentText}
""";
    }

    private DocumentSummarizationResult ParseSummaryResponse(string aiResponse, DocumentInfo documentInfo)
    {
        var result = new DocumentSummarizationResult
        {
            DocumentInfo = documentInfo,
            Confidence = 0.85 // Default confidence score
        };

        // Parse summary
        var summaryMatch = Regex.Match(aiResponse, @"SUMMARY:\s*(.*?)(?=KEY POINTS:|KEY METRICS:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (summaryMatch.Success)
        {
            result.Summary = summaryMatch.Groups[1].Value.Trim();
        }
        else
        {
            result.Summary = aiResponse; // Fallback to full response
        }

        // Parse key points
        var keyPointsMatch = Regex.Match(aiResponse, @"KEY POINTS:\s*(.*?)(?=KEY METRICS:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (keyPointsMatch.Success)
        {
            var keyPointsText = keyPointsMatch.Groups[1].Value;
            var points = Regex.Matches(keyPointsText, @"- (.*?)(?=\n|$)", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            result.KeyPoints = points;
        }

        // Parse key metrics
        var metricsMatch = Regex.Match(aiResponse, @"KEY METRICS:\s*(.*?)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (metricsMatch.Success)
        {
            var metricsText = metricsMatch.Groups[1].Value;
            var metrics = new Dictionary<string, string>();
            var metricMatches = Regex.Matches(metricsText, @"- (.*?):\s*(.*?)(?=\n|$)", RegexOptions.Multiline);
            
            foreach (Match match in metricMatches)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    metrics[key] = value;
                }
            }
            
            if (metrics.Any())
            {
                result.KeyMetrics = metrics;
            }
        }

        return result;
    }
}

/// <summary>
/// MCP tool for key information extraction
/// </summary>
public class KeyInformationExtractionTool : McpToolBase
{
    private readonly OpenAIService _openAIService;
    private readonly ILogger<KeyInformationExtractionTool> _logger;
    private readonly HttpClient _httpClient;

    public KeyInformationExtractionTool(OpenAIService openAIService, ILogger<KeyInformationExtractionTool> logger, HttpClient httpClient)
    {
        _openAIService = openAIService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public override string Name => "extract_key_information";

    public override string Description => 
        "Extract structured key information from documents including company details, financial data, legal information, and risk factors.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentUrl = new
            {
                type = "string",
                description = "URL of the document to analyze (PDF supported)"
            },
            documentContent = new
            {
                type = "string",
                description = "Direct text content to analyze (alternative to documentUrl)"
            },
            extractionType = new
            {
                type = "string",
                @enum = new[] { "company", "financial", "legal", "risk", "all" },
                description = "Type of information to extract"
            },
            maxPages = new
            {
                type = "integer",
                minimum = 1,
                maximum = 500,
                description = "Maximum number of pages to process for PDF documents"
            }
        },
        required = new[] { "extractionType" }
    };

    public async Task<McpToolResponse<KeyInformationExtractionResult>> ExecuteAsync(KeyInformationExtractionParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting key information extraction: {Type}", parameters.ExtractionType);
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<KeyInformationExtractionResult>.Error($"Validation failed: {errors}");
            }

            if (string.IsNullOrEmpty(parameters.DocumentUrl) && string.IsNullOrEmpty(parameters.DocumentContent))
            {
                return McpToolResponse<KeyInformationExtractionResult>.Error("Either documentUrl or documentContent must be provided");
            }

            string documentText;
            var documentInfo = new DocumentInfo();

            // Extract text from document
            if (!string.IsNullOrEmpty(parameters.DocumentUrl))
            {
                if (PdfProcessingService.IsPdfUrl(parameters.DocumentUrl))
                {
                    _logger.LogInformation("Processing PDF from URL: {Url}", parameters.DocumentUrl);
                    documentText = await PdfProcessingService.ExtractTextFromPdfUrlAsync(parameters.DocumentUrl, _httpClient, parameters.MaxPages);
                    documentInfo.Source = parameters.DocumentUrl;
                    documentInfo.DocumentType = "PDF";
                }
                else
                {
                    return McpToolResponse<KeyInformationExtractionResult>.Error("Only PDF URLs are currently supported");
                }
            }
            else
            {
                documentText = parameters.DocumentContent!;
                documentInfo.Source = "Direct content";
                documentInfo.DocumentType = "Text";
            }

            if (string.IsNullOrWhiteSpace(documentText))
            {
                return McpToolResponse<KeyInformationExtractionResult>.Error("No text content could be extracted from the document");
            }

            // Update document info
            documentInfo.CharacterCount = documentText.Length;
            documentInfo.ProcessingDate = DateTime.UtcNow;

            // Generate extraction using OpenAI
            var extractionPrompt = BuildExtractionPrompt(documentText, parameters);
            var aiResponse = _openAIService.GetChatResponse(extractionPrompt);

            // Parse AI response
            var result = ParseExtractionResponse(aiResponse, documentInfo, parameters.ExtractionType);
            
            _logger.LogInformation("Key information extraction completed successfully");
            return McpToolResponse<KeyInformationExtractionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during key information extraction");
            return McpToolResponse<KeyInformationExtractionResult>.Error($"Extraction failed: {ex.Message}");
        }
    }

    private string BuildExtractionPrompt(string documentText, KeyInformationExtractionParameters parameters)
    {
        var extractionInstructions = parameters.ExtractionType.ToLower() switch
        {
            "company" => "Extract company information including name, ticker, industry, sector, headquarters, employee count, and business description.",
            "financial" => "Extract financial information including revenue, net income, assets, liabilities, equity, and cash.",
            "legal" => "Extract legal information including legal proceedings, regulations, compliance issues, and corporate governance.",
            "risk" => "Extract risk factors and risk-related information.",
            "all" => "Extract all types of information: company details, financial data, legal information, and risk factors.",
            _ => "Extract key information from the document."
        };

        return $"""
Please extract structured information from the following document. {extractionInstructions}

Format your response with clear sections:

COMPANY_INFO:
Name: [Company name]
Ticker: [Stock ticker]
Industry: [Industry]
Sector: [Sector]
Headquarters: [Location]
Employees: [Employee count]
Description: [Business description]

FINANCIAL_INFO:
Revenue: [Revenue figure]
Net Income: [Net income]
Total Assets: [Total assets]
Total Liabilities: [Total liabilities]
Shareholders Equity: [Equity]
Cash: [Cash and equivalents]

LEGAL_INFO:
Legal Proceedings: [List any legal proceedings]
Regulations: [Regulatory information]
Compliance: [Compliance status]
Governance: [Corporate governance notes]

RISK_FACTORS:
- [Risk factor 1]
- [Risk factor 2]
- [Additional risk factors]

Document text:
{documentText}
""";
    }

    private KeyInformationExtractionResult ParseExtractionResponse(string aiResponse, DocumentInfo documentInfo, string extractionType)
    {
        var result = new KeyInformationExtractionResult
        {
            DocumentInfo = documentInfo
        };

        if (extractionType.ToLower() is "company" or "all")
        {
            result.CompanyInfo = ParseCompanyInfo(aiResponse);
        }

        if (extractionType.ToLower() is "financial" or "all")
        {
            result.FinancialInfo = ParseFinancialInfo(aiResponse);
        }

        if (extractionType.ToLower() is "legal" or "all")
        {
            result.LegalInfo = ParseLegalInfo(aiResponse);
        }

        if (extractionType.ToLower() is "risk" or "all")
        {
            result.RiskFactors = ParseRiskFactors(aiResponse);
        }

        return result;
    }

    private CompanyInformation? ParseCompanyInfo(string aiResponse)
    {
        var companySection = ExtractSection(aiResponse, "COMPANY_INFO");
        if (string.IsNullOrEmpty(companySection)) return null;

        return new CompanyInformation
        {
            Name = ExtractField(companySection, "Name"),
            Ticker = ExtractField(companySection, "Ticker"),
            Industry = ExtractField(companySection, "Industry"),
            Sector = ExtractField(companySection, "Sector"),
            Headquarters = ExtractField(companySection, "Headquarters"),
            EmployeeCount = ExtractField(companySection, "Employees"),
            BusinessDescription = ExtractField(companySection, "Description")
        };
    }

    private FinancialInformation? ParseFinancialInfo(string aiResponse)
    {
        var financialSection = ExtractSection(aiResponse, "FINANCIAL_INFO");
        if (string.IsNullOrEmpty(financialSection)) return null;

        return new FinancialInformation
        {
            Revenue = ExtractField(financialSection, "Revenue"),
            NetIncome = ExtractField(financialSection, "Net Income"),
            TotalAssets = ExtractField(financialSection, "Total Assets"),
            TotalLiabilities = ExtractField(financialSection, "Total Liabilities"),
            ShareholdersEquity = ExtractField(financialSection, "Shareholders Equity"),
            CashAndEquivalents = ExtractField(financialSection, "Cash")
        };
    }

    private LegalInformation? ParseLegalInfo(string aiResponse)
    {
        var legalSection = ExtractSection(aiResponse, "LEGAL_INFO");
        if (string.IsNullOrEmpty(legalSection)) return null;

        var legalProceedings = ExtractField(legalSection, "Legal Proceedings");
        var regulations = ExtractField(legalSection, "Regulations");

        return new LegalInformation
        {
            LegalProceedings = !string.IsNullOrEmpty(legalProceedings) ? new List<string> { legalProceedings } : null,
            Regulations = !string.IsNullOrEmpty(regulations) ? new List<string> { regulations } : null,
            Compliance = ExtractField(legalSection, "Compliance"),
            CorporateGovernance = ExtractField(legalSection, "Governance")
        };
    }

    private List<string>? ParseRiskFactors(string aiResponse)
    {
        var riskSection = ExtractSection(aiResponse, "RISK_FACTORS");
        if (string.IsNullOrEmpty(riskSection)) return null;

        var risks = Regex.Matches(riskSection, @"- (.*?)(?=\n|$)", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        return risks.Any() ? risks : null;
    }

    private string? ExtractSection(string text, string sectionName)
    {
        var pattern = $@"{sectionName}:\s*(.*?)(?=\n[A-Z_]+:|$)";
        var match = Regex.Match(text, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractField(string section, string fieldName)
    {
        var pattern = $@"{fieldName}:\s*(.*?)(?=\n|$)";
        var match = Regex.Match(section, pattern, RegexOptions.IgnoreCase);
        var value = match.Success ? match.Groups[1].Value.Trim() : null;
        return string.IsNullOrWhiteSpace(value) || value == "[Not specified]" || value.StartsWith("[") ? null : value;
    }
}

/// <summary>
/// MCP tool for financial data extraction
/// </summary>
public class FinancialDataExtractionTool : McpToolBase
{
    private readonly OpenAIService _openAIService;
    private readonly ILogger<FinancialDataExtractionTool> _logger;
    private readonly HttpClient _httpClient;

    public FinancialDataExtractionTool(OpenAIService openAIService, ILogger<FinancialDataExtractionTool> logger, HttpClient httpClient)
    {
        _openAIService = openAIService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public override string Name => "extract_financial_data";

    public override string Description => 
        "Extract detailed financial data from documents including financial statements, metrics, ratios, and trends analysis.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            documentUrl = new
            {
                type = "string",
                description = "URL of the document to analyze (PDF supported)"
            },
            documentContent = new
            {
                type = "string",
                description = "Direct text content to analyze (alternative to documentUrl)"
            },
            extractionScope = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Scope of financial data to extract (revenue, profit, assets, liabilities, ratios, all)"
            },
            currency = new
            {
                type = "string",
                description = "Expected currency for financial data (default: USD)"
            },
            maxPages = new
            {
                type = "integer",
                minimum = 1,
                maximum = 500,
                description = "Maximum number of pages to process for PDF documents"
            }
        }
    };

    public async Task<McpToolResponse<FinancialDataExtractionResult>> ExecuteAsync(FinancialDataExtractionParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting financial data extraction");
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<FinancialDataExtractionResult>.Error($"Validation failed: {errors}");
            }

            if (string.IsNullOrEmpty(parameters.DocumentUrl) && string.IsNullOrEmpty(parameters.DocumentContent))
            {
                return McpToolResponse<FinancialDataExtractionResult>.Error("Either documentUrl or documentContent must be provided");
            }

            string documentText;
            var documentInfo = new DocumentInfo();

            // Extract text from document
            if (!string.IsNullOrEmpty(parameters.DocumentUrl))
            {
                if (PdfProcessingService.IsPdfUrl(parameters.DocumentUrl))
                {
                    _logger.LogInformation("Processing PDF from URL: {Url}", parameters.DocumentUrl);
                    documentText = await PdfProcessingService.ExtractTextFromPdfUrlAsync(parameters.DocumentUrl, _httpClient, parameters.MaxPages);
                    documentInfo.Source = parameters.DocumentUrl;
                    documentInfo.DocumentType = "PDF";
                }
                else
                {
                    return McpToolResponse<FinancialDataExtractionResult>.Error("Only PDF URLs are currently supported");
                }
            }
            else
            {
                documentText = parameters.DocumentContent!;
                documentInfo.Source = "Direct content";
                documentInfo.DocumentType = "Text";
            }

            if (string.IsNullOrWhiteSpace(documentText))
            {
                return McpToolResponse<FinancialDataExtractionResult>.Error("No text content could be extracted from the document");
            }

            // Update document info
            documentInfo.CharacterCount = documentText.Length;
            documentInfo.ProcessingDate = DateTime.UtcNow;

            // Generate extraction using OpenAI
            var extractionPrompt = BuildFinancialExtractionPrompt(documentText, parameters);
            var aiResponse = _openAIService.GetChatResponse(extractionPrompt);

            // Parse AI response
            var result = ParseFinancialExtractionResponse(aiResponse, documentInfo, parameters.Currency);
            
            _logger.LogInformation("Financial data extraction completed successfully");
            return McpToolResponse<FinancialDataExtractionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during financial data extraction");
            return McpToolResponse<FinancialDataExtractionResult>.Error($"Financial extraction failed: {ex.Message}");
        }
    }

    private string BuildFinancialExtractionPrompt(string documentText, FinancialDataExtractionParameters parameters)
    {
        var scopeText = parameters.ExtractionScope?.Any() == true 
            ? $"Focus on these areas: {string.Join(", ", parameters.ExtractionScope)}." 
            : "Extract all available financial data.";

        return $"""
Please extract detailed financial data from the following document. {scopeText} 
Present all monetary values in {parameters.Currency} and include the reporting period where available.

Format your response with these sections:

FINANCIAL_STATEMENTS:
Income Statement:
- Revenue: [amount]
- Gross Profit: [amount]
- Operating Income: [amount]
- Net Income: [amount]

Balance Sheet:
- Total Assets: [amount]
- Total Liabilities: [amount]
- Shareholders Equity: [amount]
- Cash and Equivalents: [amount]

Cash Flow Statement:
- Operating Cash Flow: [amount]
- Investing Cash Flow: [amount]
- Financing Cash Flow: [amount]
- Free Cash Flow: [amount]

KEY_METRICS:
- Revenue: [amount]
- Earnings Per Share: [amount]
- Total Debt: [amount]

FINANCIAL_RATIOS:
- Current Ratio: [ratio]
- Debt-to-Equity: [ratio]
- Return on Assets: [percentage]
- Return on Equity: [percentage]
- Profit Margin: [percentage]
- Gross Margin: [percentage]

TRENDS:
- [Metric name]: [direction] - [description] - [significance level]

REPORTING_PERIOD: [Quarter/Year and date]

Document text:
{documentText}
""";
    }

    private FinancialDataExtractionResult ParseFinancialExtractionResponse(string aiResponse, DocumentInfo documentInfo, string currency)
    {
        var result = new FinancialDataExtractionResult
        {
            DocumentInfo = documentInfo,
            Currency = currency
        };

        // Parse reporting period
        var reportingPeriodMatch = Regex.Match(aiResponse, @"REPORTING_PERIOD:\s*(.*?)(?=\n|$)", RegexOptions.IgnoreCase);
        if (reportingPeriodMatch.Success)
        {
            result.ReportingPeriod = reportingPeriodMatch.Groups[1].Value.Trim();
        }

        // Parse financial statements
        result.FinancialStatements = ParseFinancialStatements(aiResponse);

        // Parse key metrics
        result.KeyMetrics = ParseKeyMetrics(aiResponse);

        // Parse ratios
        result.Ratios = ParseFinancialRatios(aiResponse);

        // Parse trends
        result.Trends = ParseFinancialTrends(aiResponse);

        return result;
    }

    private FinancialStatements? ParseFinancialStatements(string aiResponse)
    {
        var statementsSection = ExtractSection(aiResponse, "FINANCIAL_STATEMENTS");
        if (string.IsNullOrEmpty(statementsSection)) return null;

        var incomeStatement = new Dictionary<string, string>();
        var balanceSheet = new Dictionary<string, string>();
        var cashFlowStatement = new Dictionary<string, string>();

        // Parse Income Statement
        var incomeMatch = Regex.Match(statementsSection, @"Income Statement:(.*?)(?=Balance Sheet:|Cash Flow Statement:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (incomeMatch.Success)
        {
            ParseFinancialSection(incomeMatch.Groups[1].Value, incomeStatement);
        }

        // Parse Balance Sheet
        var balanceMatch = Regex.Match(statementsSection, @"Balance Sheet:(.*?)(?=Cash Flow Statement:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (balanceMatch.Success)
        {
            ParseFinancialSection(balanceMatch.Groups[1].Value, balanceSheet);
        }

        // Parse Cash Flow Statement
        var cashFlowMatch = Regex.Match(statementsSection, @"Cash Flow Statement:(.*?)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (cashFlowMatch.Success)
        {
            ParseFinancialSection(cashFlowMatch.Groups[1].Value, cashFlowStatement);
        }

        return new FinancialStatements
        {
            IncomeStatement = incomeStatement.Any() ? incomeStatement : null,
            BalanceSheet = balanceSheet.Any() ? balanceSheet : null,
            CashFlowStatement = cashFlowStatement.Any() ? cashFlowStatement : null
        };
    }

    private void ParseFinancialSection(string sectionText, Dictionary<string, string> dictionary)
    {
        var matches = Regex.Matches(sectionText, @"- (.*?):\s*(.*?)(?=\n|$)", RegexOptions.Multiline);
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value) && !value.StartsWith("["))
            {
                dictionary[key] = value;
            }
        }
    }

    private FinancialMetrics? ParseKeyMetrics(string aiResponse)
    {
        var metricsSection = ExtractSection(aiResponse, "KEY_METRICS");
        if (string.IsNullOrEmpty(metricsSection)) return null;

        return new FinancialMetrics
        {
            Revenue = ExtractField(metricsSection, "Revenue"),
            EarningsPerShare = ExtractField(metricsSection, "Earnings Per Share"),
            TotalDebt = ExtractField(metricsSection, "Total Debt")
        };
    }

    private FinancialRatios? ParseFinancialRatios(string aiResponse)
    {
        var ratiosSection = ExtractSection(aiResponse, "FINANCIAL_RATIOS");
        if (string.IsNullOrEmpty(ratiosSection)) return null;

        return new FinancialRatios
        {
            CurrentRatio = ExtractField(ratiosSection, "Current Ratio"),
            DebtToEquity = ExtractField(ratiosSection, "Debt-to-Equity"),
            ReturnOnAssets = ExtractField(ratiosSection, "Return on Assets"),
            ReturnOnEquity = ExtractField(ratiosSection, "Return on Equity"),
            ProfitMargin = ExtractField(ratiosSection, "Profit Margin"),
            GrossMargin = ExtractField(ratiosSection, "Gross Margin")
        };
    }

    private List<FinancialTrend>? ParseFinancialTrends(string aiResponse)
    {
        var trendsSection = ExtractSection(aiResponse, "TRENDS");
        if (string.IsNullOrEmpty(trendsSection)) return null;

        var trends = new List<FinancialTrend>();
        var trendMatches = Regex.Matches(trendsSection, @"- (.*?):\s*(.*?)\s*-\s*(.*?)\s*-\s*(.*?)(?=\n|$)", RegexOptions.Multiline);

        foreach (Match match in trendMatches)
        {
            trends.Add(new FinancialTrend
            {
                Metric = match.Groups[1].Value.Trim(),
                Direction = match.Groups[2].Value.Trim(),
                Description = match.Groups[3].Value.Trim(),
                Significance = match.Groups[4].Value.Trim()
            });
        }

        return trends.Any() ? trends : null;
    }

    private string? ExtractSection(string text, string sectionName)
    {
        var pattern = $@"{sectionName}:\s*(.*?)(?=\n[A-Z_]+:|$)";
        var match = Regex.Match(text, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractField(string section, string fieldName)
    {
        var pattern = $@"{fieldName}:\s*(.*?)(?=\n|$)";
        var match = Regex.Match(section, pattern, RegexOptions.IgnoreCase);
        var value = match.Success ? match.Groups[1].Value.Trim() : null;
        return string.IsNullOrWhiteSpace(value) || value.StartsWith("[") ? null : value;
    }
}

/// <summary>
/// MCP tool for PDF processing and text extraction
/// </summary>
public class PdfProcessingTool : McpToolBase
{
    private readonly ILogger<PdfProcessingTool> _logger;
    private readonly HttpClient _httpClient;

    public PdfProcessingTool(ILogger<PdfProcessingTool> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public override string Name => "process_pdf_document";

    public override string Description => 
        "Extract text content from PDF documents with options for page limits, text cleaning, and document analysis.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            pdfUrl = new
            {
                type = "string",
                description = "URL of the PDF document to process"
            },
            pdfBytes = new
            {
                type = "string",
                format = "byte",
                description = "Base64 encoded PDF bytes (alternative to pdfUrl)"
            },
            maxPages = new
            {
                type = "integer",
                minimum = 1,
                maximum = 1000,
                description = "Maximum number of pages to process"
            },
            includePageNumbers = new
            {
                type = "boolean",
                description = "Whether to include page separators in output"
            },
            cleanText = new
            {
                type = "boolean",
                description = "Whether to clean and normalize extracted text"
            }
        }
    };

    public async Task<McpToolResponse<PdfProcessingResult>> ExecuteAsync(PdfProcessingParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting PDF processing");
            
            // Validate parameters
            var validationContext = new ValidationContext(parameters);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(parameters, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                return McpToolResponse<PdfProcessingResult>.Error($"Validation failed: {errors}");
            }

            if (string.IsNullOrEmpty(parameters.PdfUrl) && (parameters.PdfBytes == null || parameters.PdfBytes.Length == 0))
            {
                return McpToolResponse<PdfProcessingResult>.Error("Either pdfUrl or pdfBytes must be provided");
            }

            string extractedText;
            var documentInfo = new DocumentInfo();

            if (!string.IsNullOrEmpty(parameters.PdfUrl))
            {
                _logger.LogInformation("Processing PDF from URL: {Url}", parameters.PdfUrl);
                extractedText = await PdfProcessingService.ExtractTextFromPdfUrlAsync(parameters.PdfUrl, _httpClient, parameters.MaxPages);
                documentInfo.Source = parameters.PdfUrl;
            }
            else
            {
                _logger.LogInformation("Processing PDF from byte array");
                extractedText = await PdfProcessingService.ExtractTextFromPdfAsync(parameters.PdfBytes!, parameters.MaxPages);
                documentInfo.Source = "Direct bytes";
            }

            // Calculate metrics
            var characterCount = extractedText.Length;
            var wordCount = extractedText.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

            var result = new PdfProcessingResult
            {
                ExtractedText = extractedText,
                CharacterCount = characterCount,
                WordCount = wordCount,
                DocumentInfo = new DocumentInfo
                {
                    Source = documentInfo.Source,
                    DocumentType = "PDF",
                    CharacterCount = characterCount,
                    ProcessingDate = DateTime.UtcNow
                }
            };

            _logger.LogInformation("PDF processing completed: {Characters} characters, {Words} words", characterCount, wordCount);
            return McpToolResponse<PdfProcessingResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PDF processing");
            return McpToolResponse<PdfProcessingResult>.Error($"PDF processing failed: {ex.Message}");
        }
    }
}