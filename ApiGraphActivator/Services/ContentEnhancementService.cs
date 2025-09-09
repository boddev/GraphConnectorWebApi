using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ApiGraphActivator.Services
{
    /// <summary>
    /// Service for enhancing SEC filing content with AI-powered analysis
    /// Currently uses pattern-based extraction but can be enhanced with Azure OpenAI
    /// </summary>
    public static class ContentEnhancementService
    {
        private static ILogger? _logger;

        public static void InitializeLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate an executive summary focusing on key business insights
        /// </summary>
        public static async Task<string> GenerateExecutiveSummaryAsync(string content, string company, string formType)
        {
            try
            {
                // For now, use advanced pattern matching
                // TODO: Replace with Azure OpenAI integration for better summaries
                var summary = GeneratePatternBasedSummary(content, company, formType);
                
                // Future: Add Azure OpenAI integration here
                // var prompt = $"Summarize this {formType} filing for {company} in 2-3 sentences focusing on key business insights:";
                // return await CallOpenAI(prompt + content.Substring(0, Math.Min(4000, content.Length)));
                
                return await Task.FromResult(summary);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error generating executive summary: {ex.Message}");
                return "Executive summary generation failed";
            }
        }

        /// <summary>
        /// Extract key financial and business terms with context
        /// </summary>
        public static List<string> ExtractKeyTermsWithContext(string content)
        {
            try
            {
                var keyTerms = new List<string>();
                
                // Enhanced patterns with more context
                var enhancedPatterns = new Dictionary<string, string>
                {
                    ["Revenue Growth"] = @"(?i)revenue\s+(?:increased|decreased|grew|declined)\s+by\s+[\d,]+\.?\d*%?[^.]{0,50}",
                    ["Profit Margins"] = @"(?i)(?:gross|net|operating)\s+margin[s]?\s+(?:of|was|were)\s+[\d,]+\.?\d*%[^.]{0,50}",
                    ["Market Position"] = @"(?i)(?:leading|largest|dominant|market\s+share|competitive\s+position)[^.]{0,100}",
                    ["Strategic Initiatives"] = @"(?i)(?:strategy|strategic|initiative|expansion|acquisition|investment)[^.]{0,80}",
                    ["Risk Factors"] = @"(?i)(?:risk|challenge|uncertainty|concern)[^.]{0,60}",
                    ["Future Outlook"] = @"(?i)(?:expect|anticipate|forecast|outlook|guidance|future)[^.]{0,80}"
                };

                foreach (var pattern in enhancedPatterns)
                {
                    var matches = Regex.Matches(content, pattern.Value, RegexOptions.IgnoreCase);
                    foreach (Match match in matches.Take(2)) // Limit to avoid overwhelming
                    {
                        if (match.Success && match.Value.Length > 20 && match.Value.Length < 200)
                        {
                            var cleanMatch = Regex.Replace(match.Value, @"\s+", " ").Trim();
                            keyTerms.Add($"{pattern.Key}: {cleanMatch}");
                        }
                    }
                }

                return keyTerms.Take(8).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error extracting key terms with context: {ex.Message}");
                return new List<string> { "Key terms extraction failed" };
            }
        }

        /// <summary>
        /// Identify and extract company competitive advantages
        /// </summary>
        public static List<string> ExtractCompetitiveAdvantages(string content)
        {
            try
            {
                var advantages = new HashSet<string>();
                
                var advantagePatterns = new[]
                {
                    @"(?i)(?:competitive\s+advantage|differentiat|unique|proprietary|patent|intellectual\s+property)[^.]{10,120}",
                    @"(?i)(?:market\s+leader|industry\s+leader|first\s+to|pioneer)[^.]{10,100}",
                    @"(?i)(?:cost\s+advantage|efficiency|economies\s+of\s+scale)[^.]{10,100}",
                    @"(?i)(?:brand\s+recognition|customer\s+loyalty|reputation)[^.]{10,100}"
                };

                foreach (var pattern in advantagePatterns)
                {
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches.Take(3))
                    {
                        if (match.Success && match.Value.Length > 15 && match.Value.Length < 150)
                        {
                            // Clean the match value by removing HTML tags and special characters
                            var cleanMatch = Regex.Replace(match.Value, @"<[^>]*>", ""); // Remove HTML tags
                            cleanMatch = Regex.Replace(cleanMatch, @"\s+", " ").Trim(); // Normalize whitespace
                            cleanMatch = Regex.Replace(cleanMatch, @"[^\w\s\-&,.%]", ""); // Remove special characters except common ones
                            
                            if (!string.IsNullOrEmpty(cleanMatch) && cleanMatch.Length > 10)
                            {
                                advantages.Add(cleanMatch);
                            }
                        }
                    }
                }

                return advantages.Take(5).Select(s => s.Trim().TrimEnd(',', ' ')).Where(s => s.Length > 5).Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error extracting competitive advantages: {ex.Message}");
                return new List<string> { "Competitive advantages extraction failed" };
            }
        }

        /// <summary>
        /// Generate a pattern-based summary focusing on the most important aspects
        /// </summary>
        private static string GeneratePatternBasedSummary(string content, string company, string formType)
        {
            try
            {
                var summaryParts = new List<string>();

                // Extract key business metrics
                var revenueMatch = Regex.Match(content, @"(?i)(?:total\s+)?revenue[^.]{0,100}\$[\d,]+(?:\.\d+)?(?:\s*(?:million|billion))?", RegexOptions.IgnoreCase);
                if (revenueMatch.Success)
                {
                    summaryParts.Add(revenueMatch.Value.Trim());
                }

                // Extract business focus/operations
                var businessMatch = Regex.Match(content, @"(?i)(?:the\s+company|we|our\s+business)\s+(?:operate|focus|specialize)[^.]{10,150}", RegexOptions.IgnoreCase);
                if (businessMatch.Success)
                {
                    summaryParts.Add(businessMatch.Value.Trim());
                }

                // Extract recent developments or outlook
                var developmentMatch = Regex.Match(content, @"(?i)(?:during\s+the\s+year|in\s+\d{4}|recently)[^.]{10,120}", RegexOptions.IgnoreCase);
                if (developmentMatch.Success)
                {
                    summaryParts.Add(developmentMatch.Value.Trim());
                }

                if (summaryParts.Any())
                {
                    var summary = string.Join(". ", summaryParts);
                    // Clean up the summary
                    summary = Regex.Replace(summary, @"\s+", " ");
                    summary = summary.Length > 400 ? summary.Substring(0, 400) + "..." : summary;
                    return $"{company} {formType}: {summary}";
                }

                return $"{company} {formType} filing - Summary not available from automated extraction";
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error generating pattern-based summary: {ex.Message}");
                return $"{company} {formType} filing - Summary generation failed";
            }
        }

        /// <summary>
        /// Placeholder for future Azure OpenAI integration
        /// </summary>
        private static async Task<string> CallOpenAI(string prompt)
        {
            // TODO: Implement Azure OpenAI integration
            // This would use Azure OpenAI to generate more sophisticated summaries
            await Task.Delay(0); // Placeholder
            throw new NotImplementedException("Azure OpenAI integration not yet implemented");
        }

        /// <summary>
        /// Extract ESG (Environmental, Social, Governance) related content
        /// </summary>
        public static List<string> ExtractESGContent(string content)
        {
            try
            {
                var esgContent = new HashSet<string>();
                
                var esgPatterns = new[]
                {
                    @"(?i)(?:environmental|sustainability|carbon|emissions|renewable)[^.]{10,120}",
                    @"(?i)(?:diversity|inclusion|employee|workforce|social\s+responsibility)[^.]{10,120}",
                    @"(?i)(?:governance|board|compliance|ethics|transparency)[^.]{10,120}"
                };

                foreach (var pattern in esgPatterns)
                {
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches.Take(2))
                    {
                        if (match.Success && match.Value.Length > 15)
                        {
                            // Clean the match value by removing HTML tags and special characters
                            var cleanMatch = Regex.Replace(match.Value, @"<[^>]*>", ""); // Remove HTML tags
                            cleanMatch = Regex.Replace(cleanMatch, @"\s+", " ").Trim(); // Normalize whitespace
                            cleanMatch = Regex.Replace(cleanMatch, @"[^\w\s\-&,.%]", ""); // Remove special characters except common ones
                            
                            if (!string.IsNullOrEmpty(cleanMatch) && cleanMatch.Length > 10)
                            {
                                esgContent.Add(cleanMatch);
                            }
                        }
                    }
                }

                return esgContent.Take(6).Select(s => s.Trim().TrimEnd(',', ' ')).Where(s => s.Length > 5).Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error extracting ESG content: {ex.Message}");
                return new List<string> { "ESG content extraction failed" };
            }
        }
    }
}
