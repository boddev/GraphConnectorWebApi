# Node.js Client Examples

This guide provides Node.js/JavaScript examples for integrating with the SEC Edgar MCP server.

## Installation

```bash
npm install axios node-fetch
# or
yarn add axios node-fetch
```

## Basic Client Implementation

```javascript
const axios = require('axios');

class SECEdgarMCPClient {
    /**
     * Node.js client for SEC Edgar MCP server
     * @param {string} baseUrl - Base URL of the MCP server
     */
    constructor(baseUrl = 'http://localhost:5236') {
        this.baseUrl = baseUrl.replace(/\/$/, '');
        this.client = axios.create({
            baseURL: this.baseUrl,
            headers: {
                'Content-Type': 'application/json',
                'User-Agent': 'SEC-Edgar-MCP-NodeJS-Client/1.0'
            },
            timeout: 30000
        });
    }

    /**
     * Discover available MCP tools
     * @returns {Promise<Array>} Array of tool definitions
     */
    async discoverTools() {
        try {
            const response = await this.client.get('/mcp/tools');
            return response.data;
        } catch (error) {
            throw this._handleError(error);
        }
    }

    /**
     * Search documents by company name
     * @param {Object} params - Search parameters
     * @param {string} params.companyName - Company name to search for
     * @param {Array<string>} [params.formTypes] - Form types to filter by
     * @param {string} [params.startDate] - Start date (YYYY-MM-DD)
     * @param {string} [params.endDate] - End date (YYYY-MM-DD)
     * @param {boolean} [params.includeContent=false] - Include document content
     * @param {number} [params.page=1] - Page number
     * @param {number} [params.pageSize=50] - Page size
     * @returns {Promise<Object>} Search results
     */
    async searchByCompany({
        companyName,
        formTypes,
        startDate,
        endDate,
        includeContent = false,
        page = 1,
        pageSize = 50
    }) {
        try {
            const payload = {
                companyName,
                includeContent,
                page,
                pageSize
            };

            if (formTypes) payload.formTypes = formTypes;
            if (startDate) payload.startDate = startDate;
            if (endDate) payload.endDate = endDate;

            const response = await this.client.post('/mcp/tools/company-search', payload);
            return response.data;
        } catch (error) {
            throw this._handleError(error);
        }
    }

    /**
     * Filter documents by form type and date range
     * @param {Object} params - Filter parameters
     * @param {Array<string>} [params.formTypes] - Form types to filter by
     * @param {Array<string>} [params.companyNames] - Company names to filter by
     * @param {string} [params.startDate] - Start date (YYYY-MM-DD)
     * @param {string} [params.endDate] - End date (YYYY-MM-DD)
     * @param {boolean} [params.includeContent=false] - Include document content
     * @param {number} [params.page=1] - Page number
     * @param {number} [params.pageSize=50] - Page size
     * @returns {Promise<Object>} Filtered results
     */
    async filterByFormAndDate({
        formTypes,
        companyNames,
        startDate,
        endDate,
        includeContent = false,
        page = 1,
        pageSize = 50
    }) {
        try {
            const payload = {
                includeContent,
                page,
                pageSize
            };

            if (formTypes) payload.formTypes = formTypes;
            if (companyNames) payload.companyNames = companyNames;
            if (startDate) payload.startDate = startDate;
            if (endDate) payload.endDate = endDate;

            const response = await this.client.post('/mcp/tools/form-filter', payload);
            return response.data;
        } catch (error) {
            throw this._handleError(error);
        }
    }

    /**
     * Search within document content
     * @param {Object} params - Search parameters
     * @param {string} params.searchText - Text to search for
     * @param {Array<string>} [params.companyNames] - Company names to limit scope
     * @param {Array<string>} [params.formTypes] - Form types to limit scope
     * @param {string} [params.startDate] - Start date (YYYY-MM-DD)
     * @param {string} [params.endDate] - End date (YYYY-MM-DD)
     * @param {boolean} [params.exactMatch=false] - Exact phrase match
     * @param {boolean} [params.caseSensitive=false] - Case sensitive search
     * @param {number} [params.page=1] - Page number
     * @param {number} [params.pageSize=50] - Page size
     * @returns {Promise<Object>} Content search results
     */
    async searchContent({
        searchText,
        companyNames,
        formTypes,
        startDate,
        endDate,
        exactMatch = false,
        caseSensitive = false,
        page = 1,
        pageSize = 50
    }) {
        try {
            const payload = {
                searchText,
                exactMatch,
                caseSensitive,
                page,
                pageSize
            };

            if (companyNames) payload.companyNames = companyNames;
            if (formTypes) payload.formTypes = formTypes;
            if (startDate) payload.startDate = startDate;
            if (endDate) payload.endDate = endDate;

            const response = await this.client.post('/mcp/tools/content-search', payload);
            return response.data;
        } catch (error) {
            throw this._handleError(error);
        }
    }

    /**
     * Handle and format errors
     * @private
     */
    _handleError(error) {
        if (error.response) {
            // Server responded with error status
            const { status, data } = error.response;
            return new Error(`HTTP ${status}: ${data?.errorMessage || data?.message || 'Unknown error'}`);
        } else if (error.request) {
            // Request was made but no response received
            return new Error('No response from server - check if MCP server is running');
        } else {
            // Something else happened
            return new Error(`Request error: ${error.message}`);
        }
    }
}

module.exports = SECEdgarMCPClient;
```

## Usage Examples

```javascript
// basic-usage.js
const SECEdgarMCPClient = require('./sec-edgar-client');

async function basicExamples() {
    const client = new SECEdgarMCPClient();

    try {
        // 1. Discover available tools
        console.log('=== Available Tools ===');
        const tools = await client.discoverTools();
        tools.forEach(tool => {
            console.log(`- ${tool.name}: ${tool.description}`);
        });

        // 2. Search Apple documents
        console.log('\n=== Apple 10-K and 10-Q Filings ===');
        const appleResults = await client.searchByCompany({
            companyName: 'Apple Inc.',
            formTypes: ['10-K', '10-Q'],
            startDate: '2023-01-01',
            pageSize: 5
        });

        if (!appleResults.isError) {
            const { content } = appleResults;
            console.log(`Found ${content.totalCount} documents`);
            content.items.slice(0, 3).forEach(doc => {
                console.log(`- ${doc.title} (${doc.filingDate.substring(0, 10)})`);
            });
        } else {
            console.error(`Error: ${appleResults.errorMessage}`);
        }

        // 3. Search for AI-related content
        console.log('\n=== AI Content Search ===');
        const aiResults = await client.searchContent({
            searchText: 'artificial intelligence',
            formTypes: ['10-K'],
            pageSize: 5
        });

        if (!aiResults.isError) {
            const { content } = aiResults;
            console.log(`Found ${content.totalCount} documents mentioning AI`);
            content.items.forEach(doc => {
                console.log(`- ${doc.companyName}: ${doc.title}`);
                if (doc.highlights && doc.highlights.length > 0) {
                    console.log(`  Highlights: ${doc.highlights.join(', ')}`);
                }
            });
        } else {
            console.error(`Error: ${aiResults.errorMessage}`);
        }

    } catch (error) {
        console.error('Client error:', error.message);
    }
}

// Run examples
basicExamples();
```

## Advanced Examples

### 1. Parallel Processing with Promise.all

```javascript
// parallel-search.js
const SECEdgarMCPClient = require('./sec-edgar-client');

async function parallelCompanyAnalysis() {
    const client = new SECEdgarMCPClient();
    const companies = ['Apple Inc.', 'Microsoft Corporation', 'Alphabet Inc.'];

    try {
        console.log('=== Parallel Company Analysis ===');
        
        // Search multiple companies in parallel
        const searchPromises = companies.map(company =>
            client.searchByCompany({
                companyName: company,
                formTypes: ['10-K'],
                startDate: '2023-01-01',
                pageSize: 10
            }).catch(error => ({ error: error.message, company }))
        );

        const results = await Promise.all(searchPromises);

        results.forEach((result, index) => {
            const company = companies[index];
            console.log(`\n${company}:`);
            
            if (result.error) {
                console.log(`  Error: ${result.error}`);
            } else if (result.isError) {
                console.log(`  Server Error: ${result.errorMessage}`);
            } else {
                const { content } = result;
                console.log(`  Total 10-K filings: ${content.totalCount}`);
                console.log('  Recent filings:');
                content.items.slice(0, 3).forEach(doc => {
                    console.log(`    - ${doc.filingDate.substring(0, 10)}`);
                });
            }
        });

    } catch (error) {
        console.error('Parallel analysis error:', error.message);
    }
}

parallelCompanyAnalysis();
```

### 2. Pagination Helper

```javascript
// pagination-helper.js
class PaginationHelper {
    constructor(client) {
        this.client = client;
    }

    /**
     * Fetch all pages of results
     * @param {Function} searchFunction - Search function to call
     * @param {Object} params - Search parameters
     * @param {number} maxPages - Maximum pages to fetch (safety limit)
     * @returns {Promise<Array>} All results
     */
    async fetchAllPages(searchFunction, params, maxPages = 10) {
        const allItems = [];
        let currentPage = 1;
        let hasNextPage = true;

        while (hasNextPage && currentPage <= maxPages) {
            console.log(`Fetching page ${currentPage}...`);
            
            const result = await searchFunction.call(this.client, {
                ...params,
                page: currentPage,
                pageSize: params.pageSize || 50
            });

            if (result.isError) {
                throw new Error(result.errorMessage);
            }

            const { content } = result;
            allItems.push(...content.items);
            
            hasNextPage = content.hasNextPage;
            currentPage++;
            
            console.log(`  Got ${content.items.length} items (total so far: ${allItems.length})`);
        }

        return allItems;
    }

    /**
     * Process results in batches with callback
     * @param {Function} searchFunction - Search function to call
     * @param {Object} params - Search parameters
     * @param {Function} processor - Function to process each batch
     * @param {number} maxPages - Maximum pages to process
     */
    async processBatches(searchFunction, params, processor, maxPages = 10) {
        let currentPage = 1;
        let hasNextPage = true;

        while (hasNextPage && currentPage <= maxPages) {
            const result = await searchFunction.call(this.client, {
                ...params,
                page: currentPage,
                pageSize: params.pageSize || 50
            });

            if (result.isError) {
                throw new Error(result.errorMessage);
            }

            const { content } = result;
            await processor(content.items, currentPage, content);
            
            hasNextPage = content.hasNextPage;
            currentPage++;
        }
    }
}

// Usage example
async function fetchAllAppleDocuments() {
    const client = new SECEdgarMCPClient();
    const helper = new PaginationHelper(client);

    try {
        const allDocs = await helper.fetchAllPages(
            client.searchByCompany,
            {
                companyName: 'Apple Inc.',
                formTypes: ['10-K', '10-Q'],
                startDate: '2022-01-01',
                pageSize: 100
            },
            5 // Max 5 pages
        );

        console.log(`\nTotal documents fetched: ${allDocs.length}`);
        
        // Group by form type
        const grouped = allDocs.reduce((acc, doc) => {
            acc[doc.formType] = (acc[doc.formType] || 0) + 1;
            return acc;
        }, {});

        console.log('Documents by form type:', grouped);

    } catch (error) {
        console.error('Fetch all error:', error.message);
    }
}

fetchAllAppleDocuments();
```

### 3. Content Analysis Pipeline

```javascript
// content-analysis.js
const SECEdgarMCPClient = require('./sec-edgar-client');

class ContentAnalyzer {
    constructor(client) {
        this.client = client;
    }

    /**
     * Analyze content trends across multiple search terms
     * @param {Array<string>} searchTerms - Terms to search for
     * @param {Object} filters - Additional filters
     * @returns {Promise<Object>} Analysis results
     */
    async analyzeContentTrends(searchTerms, filters = {}) {
        const trends = {};

        for (const term of searchTerms) {
            console.log(`Analyzing content for: "${term}"`);
            
            try {
                const results = await this.client.searchContent({
                    searchText: term,
                    formTypes: filters.formTypes || ['10-K'],
                    startDate: filters.startDate || '2023-01-01',
                    pageSize: 100
                });

                if (results.isError) {
                    trends[term] = { error: results.errorMessage };
                    continue;
                }

                const { content } = results;
                const analysis = this._analyzeResults(content.items);
                
                trends[term] = {
                    totalMentions: content.totalCount,
                    companiesAnalyzed: analysis.companiesAnalyzed,
                    topCompanies: analysis.topCompanies,
                    averageRelevance: analysis.averageRelevance,
                    dateRange: analysis.dateRange
                };

            } catch (error) {
                trends[term] = { error: error.message };
            }
        }

        return trends;
    }

    /**
     * Analyze search results
     * @private
     */
    _analyzeResults(items) {
        const companyStats = {};
        let totalRelevance = 0;
        const dates = [];

        items.forEach(doc => {
            const company = doc.companyName;
            
            if (!companyStats[company]) {
                companyStats[company] = {
                    count: 0,
                    totalRelevance: 0,
                    documents: []
                };
            }

            companyStats[company].count += 1;
            companyStats[company].totalRelevance += doc.relevanceScore;
            companyStats[company].documents.push({
                date: doc.filingDate,
                score: doc.relevanceScore,
                title: doc.title
            });

            totalRelevance += doc.relevanceScore;
            dates.push(new Date(doc.filingDate));
        });

        // Calculate averages and sort
        Object.keys(companyStats).forEach(company => {
            const stats = companyStats[company];
            stats.averageRelevance = stats.totalRelevance / stats.count;
        });

        const topCompanies = Object.entries(companyStats)
            .sort((a, b) => b[1].averageRelevance - a[1].averageRelevance)
            .slice(0, 10)
            .map(([company, stats]) => ({
                company,
                mentions: stats.count,
                averageRelevance: stats.averageRelevance
            }));

        return {
            companiesAnalyzed: Object.keys(companyStats).length,
            topCompanies,
            averageRelevance: totalRelevance / items.length,
            dateRange: {
                earliest: new Date(Math.min(...dates)).toISOString().split('T')[0],
                latest: new Date(Math.max(...dates)).toISOString().split('T')[0]
            }
        };
    }

    /**
     * Generate analysis report
     */
    generateReport(trends) {
        console.log('\n=== CONTENT ANALYSIS REPORT ===');
        
        Object.entries(trends).forEach(([term, data]) => {
            console.log(`\n--- ${term.toUpperCase()} ---`);
            
            if (data.error) {
                console.log(`Error: ${data.error}`);
                return;
            }

            console.log(`Total mentions: ${data.totalMentions}`);
            console.log(`Companies analyzed: ${data.companiesAnalyzed}`);
            console.log(`Average relevance: ${data.averageRelevance.toFixed(3)}`);
            console.log(`Date range: ${data.dateRange.earliest} to ${data.dateRange.latest}`);
            
            console.log('\nTop companies by relevance:');
            data.topCompanies.slice(0, 5).forEach((company, index) => {
                console.log(`  ${index + 1}. ${company.company}`);
                console.log(`     Mentions: ${company.mentions}, Avg Score: ${company.averageRelevance.toFixed(3)}`);
            });
        });
    }
}

// Usage
async function runContentAnalysis() {
    const client = new SECEdgarMCPClient();
    const analyzer = new ContentAnalyzer(client);

    try {
        const aiTerms = [
            'artificial intelligence',
            'machine learning',
            'AI technology',
            'neural networks'
        ];

        const trends = await analyzer.analyzeContentTrends(aiTerms, {
            formTypes: ['10-K'],
            startDate: '2023-01-01'
        });

        analyzer.generateReport(trends);

    } catch (error) {
        console.error('Analysis error:', error.message);
    }
}

runContentAnalysis();
```

### 4. Express.js API Wrapper

```javascript
// api-wrapper.js
const express = require('express');
const SECEdgarMCPClient = require('./sec-edgar-client');

const app = express();
app.use(express.json());

const mcpClient = new SECEdgarMCPClient();

// Middleware for error handling
const asyncHandler = (fn) => (req, res, next) => {
    Promise.resolve(fn(req, res, next)).catch(next);
};

// Search by company endpoint
app.post('/api/search/company', asyncHandler(async (req, res) => {
    const { companyName, ...otherParams } = req.body;
    
    if (!companyName) {
        return res.status(400).json({ error: 'companyName is required' });
    }

    const result = await mcpClient.searchByCompany({
        companyName,
        ...otherParams
    });

    if (result.isError) {
        return res.status(400).json({ error: result.errorMessage });
    }

    res.json(result);
}));

// Content search endpoint
app.post('/api/search/content', asyncHandler(async (req, res) => {
    const { searchText, ...otherParams } = req.body;
    
    if (!searchText) {
        return res.status(400).json({ error: 'searchText is required' });
    }

    const result = await mcpClient.searchContent({
        searchText,
        ...otherParams
    });

    if (result.isError) {
        return res.status(400).json({ error: result.errorMessage });
    }

    res.json(result);
}));

// Tools discovery endpoint
app.get('/api/tools', asyncHandler(async (req, res) => {
    const tools = await mcpClient.discoverTools();
    res.json(tools);
}));

// Error handling middleware
app.use((error, req, res, next) => {
    console.error('API Error:', error.message);
    res.status(500).json({ error: 'Internal server error' });
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
    console.log(`API wrapper server running on port ${PORT}`);
});
```

## Configuration and Environment Setup

```javascript
// config.js
const config = {
    development: {
        mcpServerUrl: 'http://localhost:5236',
        timeout: 30000,
        retries: 3
    },
    production: {
        mcpServerUrl: process.env.MCP_SERVER_URL || 'https://your-production-server.com',
        timeout: 60000,
        retries: 5
    }
};

const environment = process.env.NODE_ENV || 'development';
module.exports = config[environment];
```

## Testing with Jest

```javascript
// client.test.js
const SECEdgarMCPClient = require('./sec-edgar-client');
const axios = require('axios');

jest.mock('axios');
const mockedAxios = axios;

describe('SECEdgarMCPClient', () => {
    let client;

    beforeEach(() => {
        client = new SECEdgarMCPClient('http://test-server');
        mockedAxios.create.mockReturnValue(mockedAxios);
    });

    afterEach(() => {
        jest.clearAllMocks();
    });

    test('should search by company successfully', async () => {
        const mockResponse = {
            data: {
                content: { totalCount: 5, items: [] },
                isError: false
            }
        };
        mockedAxios.post.mockResolvedValue(mockResponse);

        const result = await client.searchByCompany({
            companyName: 'Apple Inc.'
        });

        expect(result.isError).toBe(false);
        expect(result.content.totalCount).toBe(5);
        expect(mockedAxios.post).toHaveBeenCalledWith(
            '/mcp/tools/company-search',
            expect.objectContaining({
                companyName: 'Apple Inc.'
            })
        );
    });

    test('should handle server errors', async () => {
        const mockError = {
            response: {
                status: 400,
                data: { errorMessage: 'Invalid company name' }
            }
        };
        mockedAxios.post.mockRejectedValue(mockError);

        await expect(client.searchByCompany({
            companyName: ''
        })).rejects.toThrow('HTTP 400: Invalid company name');
    });
});
```

This comprehensive Node.js client provides production-ready integration patterns, error handling, and scalable architecture for the SEC Edgar MCP server.