using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Service for managing prompt templates and rendering them with parameters
/// </summary>
public class PromptTemplateService
{
    private readonly ILogger<PromptTemplateService> _logger;
    private readonly Dictionary<string, PromptTemplate> _templates;

    public PromptTemplateService(ILogger<PromptTemplateService> logger)
    {
        _logger = logger;
        _templates = new Dictionary<string, PromptTemplate>();
        InitializeBuiltInTemplates();
    }

    /// <summary>
    /// Get all available prompt templates
    /// </summary>
    public IEnumerable<PromptTemplate> GetAllTemplates()
    {
        return _templates.Values.OrderBy(t => t.Category).ThenBy(t => t.Name);
    }

    /// <summary>
    /// Get templates by category
    /// </summary>
    public IEnumerable<PromptTemplate> GetTemplatesByCategory(string category)
    {
        return _templates.Values
            .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name);
    }

    /// <summary>
    /// Get a specific template by name
    /// </summary>
    public PromptTemplate? GetTemplate(string name)
    {
        _templates.TryGetValue(name, out var template);
        return template;
    }

    /// <summary>
    /// Render a template with provided parameters
    /// </summary>
    public async Task<RenderPromptResponse> RenderTemplateAsync(string templateName, Dictionary<string, object> parameters)
    {
        var template = GetTemplate(templateName);
        if (template == null)
        {
            throw new ArgumentException($"Template '{templateName}' not found", nameof(templateName));
        }

        // Validate parameters
        var validation = ValidateParameters(template, parameters);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Invalid parameters: {string.Join(", ", validation.Errors)}");
        }

        // Apply default values for missing optional parameters
        var finalParameters = ApplyDefaultValues(template, parameters);

        // Render the template
        var renderedPrompt = await RenderTemplateContentAsync(template.Template, finalParameters);

        return new RenderPromptResponse
        {
            RenderedPrompt = renderedPrompt,
            Template = template,
            Parameters = finalParameters
        };
    }

    /// <summary>
    /// Validate parameters against template requirements
    /// </summary>
    public PromptValidationResponse ValidateParameters(PromptTemplate template, Dictionary<string, object> parameters)
    {
        var response = new PromptValidationResponse { IsValid = true };

        foreach (var param in template.Parameters)
        {
            if (param.Required && !parameters.ContainsKey(param.Name))
            {
                response.IsValid = false;
                response.Errors.Add($"Required parameter '{param.Name}' is missing");
                continue;
            }

            if (parameters.ContainsKey(param.Name))
            {
                var value = parameters[param.Name];
                var validationResult = ValidateParameterValue(param, value);
                if (!validationResult.IsValid)
                {
                    response.IsValid = false;
                    response.Errors.AddRange(validationResult.Errors);
                }
            }
        }

        return response;
    }

    /// <summary>
    /// Add or update a custom template
    /// </summary>
    public void AddTemplate(PromptTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new ArgumentException("Template name cannot be empty", nameof(template));
        }

        _templates[template.Name] = template;
        _logger.LogInformation("Added/updated template: {TemplateName}", template.Name);
    }

    /// <summary>
    /// Remove a template
    /// </summary>
    public bool RemoveTemplate(string name)
    {
        var removed = _templates.Remove(name);
        if (removed)
        {
            _logger.LogInformation("Removed template: {TemplateName}", name);
        }
        return removed;
    }

    private async Task<string> RenderTemplateContentAsync(string template, Dictionary<string, object> parameters)
    {
        var result = template;

        // Replace {{parameter}} placeholders with actual values
        var pattern = @"\{\{(\w+)\}\}";
        result = Regex.Replace(result, pattern, match =>
        {
            var paramName = match.Groups[1].Value;
            if (parameters.TryGetValue(paramName, out var value))
            {
                return value?.ToString() ?? string.Empty;
            }
            return match.Value; // Keep placeholder if parameter not found
        });

        // Handle conditional blocks {{#if condition}}...{{/if}}
        var conditionalPattern = @"\{\{#if\s+(\w+)\}\}(.*?)\{\{/if\}\}";
        result = Regex.Replace(result, conditionalPattern, match =>
        {
            var conditionName = match.Groups[1].Value;
            var content = match.Groups[2].Value;
            
            if (parameters.TryGetValue(conditionName, out var value))
            {
                // Check if value is truthy
                if (IsTruthy(value))
                {
                    return content;
                }
            }
            return string.Empty;
        }, RegexOptions.Singleline);

        // Handle loops {{#each array}}...{{/each}}
        var loopPattern = @"\{\{#each\s+(\w+)\}\}(.*?)\{\{/each\}\}";
        result = Regex.Replace(result, loopPattern, match =>
        {
            var arrayName = match.Groups[1].Value;
            var itemTemplate = match.Groups[2].Value;
            
            if (parameters.TryGetValue(arrayName, out var value) && value is IEnumerable<object> items)
            {
                return string.Join("\n", items.Select((item, index) =>
                {
                    var itemContent = itemTemplate;
                    itemContent = itemContent.Replace("{{this}}", item?.ToString() ?? string.Empty);
                    itemContent = itemContent.Replace("{{@index}}", index.ToString());
                    return itemContent;
                }));
            }
            return string.Empty;
        }, RegexOptions.Singleline);

        return await Task.FromResult(result);
    }

    private Dictionary<string, object> ApplyDefaultValues(PromptTemplate template, Dictionary<string, object> parameters)
    {
        var result = new Dictionary<string, object>(parameters);

        foreach (var param in template.Parameters.Where(p => !p.Required && p.DefaultValue != null))
        {
            if (!result.ContainsKey(param.Name))
            {
                result[param.Name] = param.DefaultValue!;
            }
        }

        return result;
    }

    private PromptValidationResponse ValidateParameterValue(PromptParameter param, object value)
    {
        var response = new PromptValidationResponse { IsValid = true };

        if (param.AllowedValues?.Any() == true)
        {
            var stringValue = value?.ToString();
            if (!param.AllowedValues.Contains(stringValue))
            {
                response.IsValid = false;
                response.Errors.Add($"Parameter '{param.Name}' value must be one of: {string.Join(", ", param.AllowedValues)}");
            }
        }

        if (param.Validation != null)
        {
            var validation = param.Validation;
            var stringValue = value?.ToString();

            if (param.Type == PromptParameterType.String && stringValue != null)
            {
                if (validation.MinLength.HasValue && stringValue.Length < validation.MinLength.Value)
                {
                    response.IsValid = false;
                    response.Errors.Add($"Parameter '{param.Name}' must be at least {validation.MinLength.Value} characters");
                }

                if (validation.MaxLength.HasValue && stringValue.Length > validation.MaxLength.Value)
                {
                    response.IsValid = false;
                    response.Errors.Add($"Parameter '{param.Name}' must be at most {validation.MaxLength.Value} characters");
                }

                if (!string.IsNullOrEmpty(validation.Pattern) && !Regex.IsMatch(stringValue, validation.Pattern))
                {
                    response.IsValid = false;
                    response.Errors.Add($"Parameter '{param.Name}' does not match required pattern");
                }
            }

            if (param.Type == PromptParameterType.Number && value != null)
            {
                if (double.TryParse(value.ToString(), out var numValue))
                {
                    if (validation.Minimum.HasValue && numValue < validation.Minimum.Value)
                    {
                        response.IsValid = false;
                        response.Errors.Add($"Parameter '{param.Name}' must be at least {validation.Minimum.Value}");
                    }

                    if (validation.Maximum.HasValue && numValue > validation.Maximum.Value)
                    {
                        response.IsValid = false;
                        response.Errors.Add($"Parameter '{param.Name}' must be at most {validation.Maximum.Value}");
                    }
                }
                else
                {
                    response.IsValid = false;
                    response.Errors.Add($"Parameter '{param.Name}' must be a valid number");
                }
            }
        }

        return response;
    }

    private bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool boolValue) return boolValue;
        if (value is string stringValue) return !string.IsNullOrWhiteSpace(stringValue);
        if (value is int intValue) return intValue != 0;
        if (value is double doubleValue) return doubleValue != 0.0;
        return true;
    }

    private void InitializeBuiltInTemplates()
    {
        // Document Analysis Templates
        AddTemplate(new PromptTemplate
        {
            Name = "document-summary",
            Description = "Generate a comprehensive summary of SEC filing documents",
            Category = PromptCategories.DocumentAnalysis,
            Template = """
You are a financial document analyst. Please analyze the following SEC filing document and provide a comprehensive summary.

Document Type: {{documentType}}
Company: {{companyName}}
Filing Date: {{filingDate}}

Document Content:
{{documentContent}}

Please provide a structured summary including:
1. Key business developments
2. Financial highlights
3. Risk factors mentioned
4. Forward-looking statements
5. Material changes from previous filings

{{#if includeMetrics}}
Include specific financial metrics and ratios where available.
{{/if}}

{{#if focusAreas}}
Pay special attention to these areas:
{{#each focusAreas}}
- {{this}}
{{/each}}
{{/if}}

Keep the summary concise but comprehensive, suitable for executive review.
""",
            Parameters = new List<PromptParameter>
            {
                new() { Name = "documentType", Description = "Type of SEC filing (e.g., 10-K, 10-Q, 8-K)", Type = PromptParameterType.String, Required = true },
                new() { Name = "companyName", Description = "Name of the company", Type = PromptParameterType.String, Required = true },
                new() { Name = "filingDate", Description = "Date the document was filed", Type = PromptParameterType.String, Required = true },
                new() { Name = "documentContent", Description = "Content of the document to analyze", Type = PromptParameterType.String, Required = true },
                new() { Name = "includeMetrics", Description = "Whether to include detailed financial metrics", Type = PromptParameterType.Boolean, Required = false, DefaultValue = "false" },
                new() { Name = "focusAreas", Description = "Specific areas to focus on in the analysis", Type = PromptParameterType.Array, Required = false }
            },
            Tags = new List<string> { "summary", "analysis", "sec-filing" }
        });

        AddTemplate(new PromptTemplate
        {
            Name = "financial-data-extraction",
            Description = "Extract specific financial data points from SEC documents",
            Category = PromptCategories.FinancialExtraction,
            Template = """
You are a financial data extraction specialist. Extract the following financial information from the SEC filing document provided.

Company: {{companyName}}
Document Type: {{documentType}}
Reporting Period: {{reportingPeriod}}

Document Content:
{{documentContent}}

Please extract the following financial data points:
{{#each dataPoints}}
- {{this}}
{{/each}}

Format the extracted data as a structured JSON object with the following format:
{
  "company": "{{companyName}}",
  "reportingPeriod": "{{reportingPeriod}}",
  "extractedData": {
    // key-value pairs for each requested data point
  },
  "confidence": "high|medium|low",
  "notes": "any important observations or caveats"
}

Only include data that is explicitly stated in the document. If a data point is not found, mark it as "not available" or "N/A".
""",
            Parameters = new List<PromptParameter>
            {
                new() { Name = "companyName", Description = "Name of the company", Type = PromptParameterType.String, Required = true },
                new() { Name = "documentType", Description = "Type of SEC filing", Type = PromptParameterType.String, Required = true },
                new() { Name = "reportingPeriod", Description = "Reporting period for the financial data", Type = PromptParameterType.String, Required = true },
                new() { Name = "documentContent", Description = "Content of the document to extract from", Type = PromptParameterType.String, Required = true },
                new() { Name = "dataPoints", Description = "List of specific financial data points to extract", Type = PromptParameterType.Array, Required = true }
            },
            Tags = new List<string> { "extraction", "financial-data", "structured-output" }
        });

        AddTemplate(new PromptTemplate
        {
            Name = "company-comparison",
            Description = "Compare financial and business metrics between companies",
            Category = PromptCategories.ComparisonSummarization,
            Template = """
You are a financial analyst specializing in company comparisons. Compare the following companies based on their SEC filing documents.

{{#each companies}}
Company {{@index}}:
Name: {{name}}
Document Type: {{documentType}}
Filing Date: {{filingDate}}
Content: {{content}}

{{/each}}

Please provide a comprehensive comparison analysis including:

1. **Financial Performance Comparison**
   - Revenue growth and trends
   - Profitability metrics
   - Cash flow analysis
   - Debt and liquidity position

2. **Business Strategy Comparison**
   - Market positioning
   - Growth strategies
   - Investment priorities
   - Risk management approaches

3. **Operational Metrics**
   - Efficiency indicators
   - Market share trends
   - Customer metrics (if available)

{{#if comparisonCriteria}}
Focus particularly on these comparison criteria:
{{#each comparisonCriteria}}
- {{this}}
{{/each}}
{{/if}}

4. **Key Insights and Recommendations**
   - Relative strengths and weaknesses
   - Investment implications
   - Risk considerations

Present the analysis in a clear, structured format suitable for investment decision-making.
""",
            Parameters = new List<PromptParameter>
            {
                new() { Name = "companies", Description = "Array of company data objects to compare", Type = PromptParameterType.Array, Required = true },
                new() { Name = "comparisonCriteria", Description = "Specific criteria to focus on in the comparison", Type = PromptParameterType.Array, Required = false }
            },
            Tags = new List<string> { "comparison", "analysis", "investment" }
        });

        AddTemplate(new PromptTemplate
        {
            Name = "risk-assessment",
            Description = "Identify and assess risks mentioned in SEC filings",
            Category = PromptCategories.DocumentAnalysis,
            Template = """
You are a risk assessment specialist. Analyze the SEC filing document to identify, categorize, and assess risks mentioned by the company.

Company: {{companyName}}
Document Type: {{documentType}}
Filing Date: {{filingDate}}

Document Content:
{{documentContent}}

Please provide a comprehensive risk assessment with the following structure:

1. **Risk Identification**
   - List all risks explicitly mentioned in the document
   - Categorize risks by type (operational, financial, regulatory, market, etc.)

2. **Risk Assessment**
   - Assess the potential impact of each risk (High/Medium/Low)
   - Evaluate the likelihood of occurrence
   - Identify any new risks compared to previous filings

3. **Risk Mitigation**
   - Document any mitigation strategies mentioned by the company
   - Assess the adequacy of proposed mitigation measures

{{#if riskCategories}}
Focus particularly on these risk categories:
{{#each riskCategories}}
- {{this}}
{{/each}}
{{/if}}

4. **Overall Risk Profile**
   - Summarize the company's overall risk exposure
   - Highlight the most significant concerns
   - Compare risk profile to industry norms (if mentioned)

Format the assessment as a structured analysis suitable for risk management review.
""",
            Parameters = new List<PromptParameter>
            {
                new() { Name = "companyName", Description = "Name of the company", Type = PromptParameterType.String, Required = true },
                new() { Name = "documentType", Description = "Type of SEC filing", Type = PromptParameterType.String, Required = true },
                new() { Name = "filingDate", Description = "Date the document was filed", Type = PromptParameterType.String, Required = true },
                new() { Name = "documentContent", Description = "Content of the document to analyze", Type = PromptParameterType.String, Required = true },
                new() { Name = "riskCategories", Description = "Specific risk categories to focus on", Type = PromptParameterType.Array, Required = false }
            },
            Tags = new List<string> { "risk-assessment", "compliance", "analysis" }
        });

        AddTemplate(new PromptTemplate
        {
            Name = "quarterly-trend-analysis",
            Description = "Analyze trends across multiple quarterly reports",
            Category = PromptCategories.ComparisonSummarization,
            Template = """
You are a financial trend analyst. Analyze the quarterly trends for the company based on multiple SEC filings.

Company: {{companyName}}
Analysis Period: {{periodStart}} to {{periodEnd}}

Quarterly Reports:
{{#each quarters}}
Quarter {{@index}}:
Period: {{period}}
Document Type: {{documentType}}
Key Data: {{content}}

{{/each}}

Please provide a comprehensive trend analysis including:

1. **Financial Trends**
   - Revenue growth trajectory
   - Profitability trends
   - Cash flow patterns
   - Balance sheet evolution

2. **Business Performance Trends**
   - Operational efficiency changes
   - Market share evolution
   - Customer metrics trends

3. **Strategic Direction Analysis**
   - Investment pattern changes
   - Strategic initiative progress
   - Management guidance evolution

{{#if trendMetrics}}
Focus on these specific trend metrics:
{{#each trendMetrics}}
- {{this}}
{{/each}}
{{/if}}

4. **Forward-Looking Insights**
   - Trajectory implications
   - Potential inflection points
   - Seasonal patterns identified

5. **Key Observations**
   - Most significant trends
   - Concerning patterns
   - Positive developments

Present the analysis with quantitative insights where possible and highlight the most actionable trends.
""",
            Parameters = new List<PromptParameter>
            {
                new() { Name = "companyName", Description = "Name of the company", Type = PromptParameterType.String, Required = true },
                new() { Name = "periodStart", Description = "Start of the analysis period", Type = PromptParameterType.String, Required = true },
                new() { Name = "periodEnd", Description = "End of the analysis period", Type = PromptParameterType.String, Required = true },
                new() { Name = "quarters", Description = "Array of quarterly report data", Type = PromptParameterType.Array, Required = true },
                new() { Name = "trendMetrics", Description = "Specific metrics to focus on for trend analysis", Type = PromptParameterType.Array, Required = false }
            },
            Tags = new List<string> { "trends", "quarterly", "analysis", "time-series" }
        });

        _logger.LogInformation("Initialized {TemplateCount} built-in prompt templates", _templates.Count);
    }
}