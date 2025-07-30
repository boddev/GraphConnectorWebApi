using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ApiGraphActivator.Services;

public static class PdfProcessingService
{
    private static ILogger? _logger;

    public static void InitializeLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts text content from a PDF byte array
    /// </summary>
    /// <param name="pdfBytes">The PDF content as byte array</param>
    /// <param name="maxPages">Maximum number of pages to process (default: 100)</param>
    /// <returns>Extracted text content</returns>
    public static async Task<string> ExtractTextFromPdfAsync(byte[] pdfBytes, int maxPages = 100)
    {
        try
        {
            _logger?.LogTrace("Starting PDF text extraction");
            
            // Run PDF processing on a background thread since it's CPU-intensive
            return await Task.Run(() =>
            {
                using var memoryStream = new MemoryStream(pdfBytes);
                using var pdfReader = new PdfReader(memoryStream);
                using var pdfDocument = new PdfDocument(pdfReader);
                
                var textBuilder = new StringBuilder();
                int pageCount = Math.Min(pdfDocument.GetNumberOfPages(), maxPages);
                
                _logger?.LogTrace($"Processing {pageCount} pages from PDF document");
                
                for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                {
                    try
                    {
                        var page = pdfDocument.GetPage(pageNum);
                        var strategy = new SimpleTextExtractionStrategy();
                        var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                        
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            // Add page separator
                            if (textBuilder.Length > 0)
                            {
                                textBuilder.AppendLine("\n--- PAGE " + pageNum + " ---\n");
                            }
                            
                            textBuilder.AppendLine(pageText);
                        }
                        
                        _logger?.LogTrace($"Extracted text from page {pageNum} ({pageText?.Length ?? 0} characters)");
                    }
                    catch (Exception pageEx)
                    {
                        _logger?.LogWarning($"Error extracting text from page {pageNum}: {pageEx.Message}");
                        continue; // Skip problematic pages but continue with others
                    }
                }
                
                string extractedText = textBuilder.ToString();
                
                // Clean up the extracted text
                extractedText = CleanExtractedText(extractedText);
                
                _logger?.LogInformation($"PDF text extraction completed. Total characters extracted: {extractedText.Length}");
                
                return extractedText;
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error extracting text from PDF: {ex.Message}");
            throw new InvalidOperationException($"Failed to extract text from PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts text content from a PDF downloaded from a URL
    /// </summary>
    /// <param name="pdfUrl">URL of the PDF document</param>
    /// <param name="httpClient">HttpClient instance for downloading</param>
    /// <param name="maxPages">Maximum number of pages to process</param>
    /// <returns>Extracted text content</returns>
    public static async Task<string> ExtractTextFromPdfUrlAsync(string pdfUrl, HttpClient httpClient, int maxPages = 100)
    {
        try
        {
            _logger?.LogTrace($"Downloading PDF from URL: {pdfUrl}");
            
            var response = await httpClient.GetAsync(pdfUrl);
            response.EnsureSuccessStatusCode();
            
            var pdfBytes = await response.Content.ReadAsByteArrayAsync();
            _logger?.LogTrace($"Downloaded PDF ({pdfBytes.Length} bytes)");
            
            return await ExtractTextFromPdfAsync(pdfBytes, maxPages);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error downloading and extracting PDF from URL {pdfUrl}: {ex.Message}");
            throw new InvalidOperationException($"Failed to download and extract PDF from URL: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cleans up extracted PDF text by removing unwanted formatting and characters
    /// </summary>
    /// <param name="text">Raw extracted text</param>
    /// <returns>Cleaned text</returns>
    private static string CleanExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove excessive whitespace and normalize line breaks
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[\r\n]+", "\n");
        
        // Remove common PDF artifacts
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\f", ""); // Form feed characters
        text = System.Text.RegularExpressions.Regex.Replace(text, @"Page \d+ of \d+", ""); // Page numbering
        text = System.Text.RegularExpressions.Regex.Replace(text, @"Table of Contents", ""); // Common headers
        
        // Remove checkbox symbols that might appear in PDFs
        text = text.Replace("☐", ""); // Empty checkbox
        text = text.Replace("☑", ""); // Checked checkbox
        text = text.Replace("☒", ""); // X-marked checkbox
        text = text.Replace("✓", ""); // Checkmark
        text = text.Replace("✗", ""); // X mark
        
        // Remove common financial document artifacts
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b\d{1,3}(,\d{3})*\.\d{2}\b", " [NUMBER] "); // Dollar amounts
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b\d{10,}\b", ""); // Long ID numbers
        
        // Trim and normalize
        text = text.Trim();
        
        return text;
    }

    /// <summary>
    /// Checks if a URL points to a PDF document
    /// </summary>
    /// <param name="url">URL to check</param>
    /// <returns>True if URL appears to be a PDF</returns>
    public static bool IsPdfUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url) && url.ToLowerInvariant().Contains(".pdf");
    }

    /// <summary>
    /// Validates if the byte array contains a valid PDF
    /// </summary>
    /// <param name="data">Byte array to validate</param>
    /// <returns>True if data appears to be a valid PDF</returns>
    public static bool IsValidPdf(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;

        // Check for PDF header signature
        return data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46; // %PDF
    }
}
