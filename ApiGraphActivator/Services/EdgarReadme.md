# EdgarService - Step-by-Step Overview

## 1. Data Discovery Phase
The `HydrateLookupData` method starts the main data gathering process:

### Company Information Collection
- Fetches the SEC's master company directory from `https://www.sec.gov/files/company_tickers.json` - this contains all publicly traded companies and their ticker symbols
- Retrieves a predefined list of target companies from Azure Table Storage 
- For each company in the list, looks up their unique CIK (Central Index Key) identifier using the `ExtractCIK` method

### SEC Filing Discovery
- Once a company's CIK is found, calls `GetCIKFiling` to discover all available filings for that company
- Accesses the SEC's submissions API at: `https://data.sec.gov/submissions/CIK{paddedCIK}.json`
- This endpoint returns comprehensive metadata about all filings submitted by the company

## 2. Filing Processing and Tracking
The `GetDocument` method processes the discovered filings:

### Filing Analysis and Filtering
- Parses the SEC response to extract key details: filing dates, form types (10-K, 10-Q, 8-K, DEF 14A), document names, and reference numbers
- Applies business rules to filter filings:
  - Only processes filings from the last few years (configurable)
  - Focuses on major regulatory forms (annual reports, quarterly reports, current reports, proxy statements)

### Data Inventory Management
- Uses `InsertItemIfNotExists` to create an inventory of available filings in Azure Table Storage
- Each filing gets tracked with metadata and a "Processed" status flag
- This prevents duplicate work and provides visibility into what data is available vs. what's been processed

## 3. Document Content Extraction
### Processing Queue Management
- Queries for unprocessed filings using `QueryUnprocessedData`
- Creates unique identifiers for each document to avoid conflicts
- Builds the actual document URLs pointing to SEC's EDGAR database: `https://www.sec.gov/Archives/edgar/data/{cik}/{accessionNumber}/{primaryDocument}`

### Content Retrieval and Processing
- **Document Download**: Uses `FetchWithExponentialBackoff` to download filing documents with intelligent retry logic
- **Content Extraction**: Converts HTML-formatted SEC filings into plain text using `ExtractTextFromHtml`
- **Format Handling**: Skips PDF documents and focuses on HTML/text filings for better text extraction

## 4. Microsoft Graph Integration
### Data Preparation
- Creates structured `EdgarExternalItem` objects containing the extracted content and metadata
- Calls `ContentService.Transform` to format data for Microsoft Graph connectivity

### Processing Status Updates
- Updates the tracking system using `UpdateProcessedItem` to mark successfully processed documents
- Maintains a complete audit trail of processing activities

## 5. Error Handling and Reliability
### SEC API Compliance
- **Rate Limiting**: Implements exponential backoff strategy to respect SEC's usage limits
- **Retry Logic**: Honors "Retry-After" headers from SEC responses and implements smart retry patterns
- **Graceful Failures**: Continues processing other documents when individual items fail

### System Resilience
- Comprehensive logging throughout the process for monitoring and troubleshooting
- Transactional updates to maintain data consistency
- Recovery capabilities allow restarting from where processing left off

## Key Data Flow Summary
1. **Discovery** → Identify companies and fetch their filing metadata from SEC APIs
2. **Inventory** → Catalog all available filings in Azure storage with processing status
3. **Processing** → Download and extract text content from unprocessed SEC documents  
4. **Integration** → Transform content and load into Microsoft Graph search index
5. **Tracking** → Update processing status to enable resumable operations

This approach ensures efficient processing of large volumes of SEC regulatory data while maintaining compliance with SEC API policies and providing full visibility into the data pipeline.