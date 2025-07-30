# Python Client Examples

This guide provides Python examples for integrating with the SEC Edgar MCP server.

## Installation

```bash
pip install requests asyncio aiohttp
```

## Basic Synchronous Client

```python
import requests
import json
from typing import List, Optional, Dict, Any
from datetime import datetime

class SECEdgarMCPClient:
    """Synchronous Python client for SEC Edgar MCP server"""
    
    def __init__(self, base_url: str = "http://localhost:5236"):
        self.base_url = base_url.rstrip('/')
        self.session = requests.Session()
        self.session.headers.update({
            'Content-Type': 'application/json',
            'User-Agent': 'SEC-Edgar-MCP-Python-Client/1.0'
        })
    
    def discover_tools(self) -> List[Dict[str, Any]]:
        """Discover available MCP tools"""
        response = self.session.get(f"{self.base_url}/mcp/tools")
        response.raise_for_status()
        return response.json()
    
    def search_by_company(
        self,
        company_name: str,
        form_types: Optional[List[str]] = None,
        start_date: Optional[str] = None,
        end_date: Optional[str] = None,
        include_content: bool = False,
        page: int = 1,
        page_size: int = 50
    ) -> Dict[str, Any]:
        """Search documents by company name"""
        payload = {
            "companyName": company_name,
            "includeContent": include_content,
            "page": page,
            "pageSize": page_size
        }
        
        if form_types:
            payload["formTypes"] = form_types
        if start_date:
            payload["startDate"] = start_date
        if end_date:
            payload["endDate"] = end_date
            
        response = self.session.post(
            f"{self.base_url}/mcp/tools/company-search",
            json=payload
        )
        response.raise_for_status()
        return response.json()
    
    def filter_by_form_and_date(
        self,
        form_types: Optional[List[str]] = None,
        company_names: Optional[List[str]] = None,
        start_date: Optional[str] = None,
        end_date: Optional[str] = None,
        include_content: bool = False,
        page: int = 1,
        page_size: int = 50
    ) -> Dict[str, Any]:
        """Filter documents by form type and date range"""
        payload = {
            "includeContent": include_content,
            "page": page,
            "pageSize": page_size
        }
        
        if form_types:
            payload["formTypes"] = form_types
        if company_names:
            payload["companyNames"] = company_names
        if start_date:
            payload["startDate"] = start_date
        if end_date:
            payload["endDate"] = end_date
            
        response = self.session.post(
            f"{self.base_url}/mcp/tools/form-filter",
            json=payload
        )
        response.raise_for_status()
        return response.json()
    
    def search_content(
        self,
        search_text: str,
        company_names: Optional[List[str]] = None,
        form_types: Optional[List[str]] = None,
        start_date: Optional[str] = None,
        end_date: Optional[str] = None,
        exact_match: bool = False,
        case_sensitive: bool = False,
        page: int = 1,
        page_size: int = 50
    ) -> Dict[str, Any]:
        """Search within document content"""
        payload = {
            "searchText": search_text,
            "exactMatch": exact_match,
            "caseSensitive": case_sensitive,
            "page": page,
            "pageSize": page_size
        }
        
        if company_names:
            payload["companyNames"] = company_names
        if form_types:
            payload["formTypes"] = form_types
        if start_date:
            payload["startDate"] = start_date
        if end_date:
            payload["endDate"] = end_date
            
        response = self.session.post(
            f"{self.base_url}/mcp/tools/content-search",
            json=payload
        )
        response.raise_for_status()
        return response.json()

# Usage Examples
if __name__ == "__main__":
    client = SECEdgarMCPClient()
    
    # 1. Discover available tools
    print("=== Available Tools ===")
    tools = client.discover_tools()
    for tool in tools:
        print(f"- {tool['name']}: {tool['description']}")
    
    # 2. Search Apple documents
    print("\n=== Apple 10-K and 10-Q Filings ===")
    apple_docs = client.search_by_company(
        company_name="Apple Inc.",
        form_types=["10-K", "10-Q"],
        start_date="2023-01-01"
    )
    
    if not apple_docs["isError"]:
        content = apple_docs["content"]
        print(f"Found {content['totalCount']} documents")
        for doc in content["items"][:3]:  # Show first 3
            print(f"- {doc['title']} ({doc['filingDate'][:10]})")
    else:
        print(f"Error: {apple_docs['errorMessage']}")
    
    # 3. Search for AI-related content
    print("\n=== AI Content Search ===")
    ai_search = client.search_content(
        search_text="artificial intelligence",
        form_types=["10-K"],
        page_size=5
    )
    
    if not ai_search["isError"]:
        content = ai_search["content"]
        print(f"Found {content['totalCount']} documents mentioning AI")
        for doc in content["items"]:
            print(f"- {doc['companyName']}: {doc['title']}")
            if doc.get('highlights'):
                print(f"  Highlights: {', '.join(doc['highlights'])}")
    else:
        print(f"Error: {ai_search['errorMessage']}")
```

## Asynchronous Client

```python
import asyncio
import aiohttp
from typing import List, Optional, Dict, Any

class AsyncSECEdgarMCPClient:
    """Asynchronous Python client for SEC Edgar MCP server"""
    
    def __init__(self, base_url: str = "http://localhost:5236"):
        self.base_url = base_url.rstrip('/')
        self.session = None
    
    async def __aenter__(self):
        self.session = aiohttp.ClientSession(
            headers={
                'Content-Type': 'application/json',
                'User-Agent': 'SEC-Edgar-MCP-AsyncPython-Client/1.0'
            }
        )
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if self.session:
            await self.session.close()
    
    async def search_by_company(self, company_name: str, **kwargs) -> Dict[str, Any]:
        """Async company search"""
        payload = {"companyName": company_name, **kwargs}
        
        async with self.session.post(
            f"{self.base_url}/mcp/tools/company-search",
            json=payload
        ) as response:
            response.raise_for_status()
            return await response.json()
    
    async def search_content(self, search_text: str, **kwargs) -> Dict[str, Any]:
        """Async content search"""
        payload = {"searchText": search_text, **kwargs}
        
        async with self.session.post(
            f"{self.base_url}/mcp/tools/content-search",
            json=payload
        ) as response:
            response.raise_for_status()
            return await response.json()

# Async usage example
async def main():
    async with AsyncSECEdgarMCPClient() as client:
        # Parallel searches
        tasks = [
            client.search_by_company("Apple Inc.", form_types=["10-K"]),
            client.search_by_company("Microsoft Corporation", form_types=["10-K"]),
            client.search_content("revenue growth", form_types=["10-Q"])
        ]
        
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        for i, result in enumerate(results):
            if isinstance(result, Exception):
                print(f"Task {i} failed: {result}")
            else:
                content = result.get("content", {})
                total = content.get("totalCount", 0)
                print(f"Task {i}: Found {total} documents")

# Run async example
# asyncio.run(main())
```

## Advanced Examples

### 1. Batch Company Analysis

```python
def analyze_companies_batch(client: SECEdgarMCPClient, companies: List[str]):
    """Analyze multiple companies in batch"""
    results = {}
    
    for company in companies:
        print(f"Analyzing {company}...")
        
        # Get recent 10-K filings
        recent_10k = client.search_by_company(
            company_name=company,
            form_types=["10-K"],
            start_date="2023-01-01",
            page_size=5
        )
        
        if not recent_10k["isError"]:
            results[company] = {
                "total_10k": recent_10k["content"]["totalCount"],
                "recent_filings": [
                    {
                        "date": doc["filingDate"][:10],
                        "url": doc["url"]
                    }
                    for doc in recent_10k["content"]["items"]
                ]
            }
        else:
            results[company] = {"error": recent_10k["errorMessage"]}
    
    return results

# Usage
client = SECEdgarMCPClient()
tech_companies = ["Apple Inc.", "Microsoft Corporation", "Alphabet Inc."]
analysis = analyze_companies_batch(client, tech_companies)

for company, data in analysis.items():
    print(f"\n{company}:")
    if "error" in data:
        print(f"  Error: {data['error']}")
    else:
        print(f"  Total 10-K filings: {data['total_10k']}")
        print("  Recent filings:")
        for filing in data["recent_filings"][:3]:
            print(f"    - {filing['date']}")
```

### 2. Content Analysis Pipeline

```python
def analyze_content_trends(client: SECEdgarMCPClient, search_terms: List[str]):
    """Analyze content trends across search terms"""
    trends = {}
    
    for term in search_terms:
        print(f"Searching for '{term}'...")
        
        # Search across all recent 10-K filings
        results = client.search_content(
            search_text=term,
            form_types=["10-K"],
            start_date="2023-01-01",
            page_size=100
        )
        
        if not results["isError"]:
            content = results["content"]
            
            # Analyze by company
            company_mentions = {}
            for doc in content["items"]:
                company = doc["companyName"]
                if company not in company_mentions:
                    company_mentions[company] = {
                        "count": 0,
                        "avg_relevance": 0,
                        "documents": []
                    }
                
                company_mentions[company]["count"] += 1
                company_mentions[company]["avg_relevance"] += doc["relevanceScore"]
                company_mentions[company]["documents"].append({
                    "date": doc["filingDate"][:10],
                    "score": doc["relevanceScore"]
                })
            
            # Calculate averages
            for company in company_mentions:
                count = company_mentions[company]["count"]
                company_mentions[company]["avg_relevance"] /= count
            
            trends[term] = {
                "total_mentions": content["totalCount"],
                "companies": dict(sorted(
                    company_mentions.items(),
                    key=lambda x: x[1]["avg_relevance"],
                    reverse=True
                )[:10])  # Top 10 companies
            }
        else:
            trends[term] = {"error": results["errorMessage"]}
    
    return trends

# Usage
client = SECEdgarMCPClient()
ai_terms = ["artificial intelligence", "machine learning", "AI technology"]
trend_analysis = analyze_content_trends(client, ai_terms)

for term, data in trend_analysis.items():
    print(f"\n=== {term.upper()} ===")
    if "error" in data:
        print(f"Error: {data['error']}")
    else:
        print(f"Total mentions: {data['total_mentions']}")
        print("Top companies by relevance:")
        for company, stats in list(data["companies"].items())[:5]:
            print(f"  {company}: {stats['count']} mentions, "
                  f"avg score: {stats['avg_relevance']:.2f}")
```

### 3. Error Handling and Retry Logic

```python
import time
from functools import wraps

def retry_on_failure(max_retries=3, delay=1.0, backoff=2.0):
    """Decorator for retrying failed requests"""
    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            retries = 0
            current_delay = delay
            
            while retries < max_retries:
                try:
                    return func(*args, **kwargs)
                except requests.RequestException as e:
                    retries += 1
                    if retries >= max_retries:
                        raise e
                    
                    print(f"Request failed (attempt {retries}/{max_retries}): {e}")
                    print(f"Retrying in {current_delay} seconds...")
                    time.sleep(current_delay)
                    current_delay *= backoff
            
            return None
        return wrapper
    return decorator

class RobustSECEdgarMCPClient(SECEdgarMCPClient):
    """Client with enhanced error handling"""
    
    @retry_on_failure(max_retries=3)
    def search_by_company(self, *args, **kwargs):
        return super().search_by_company(*args, **kwargs)
    
    @retry_on_failure(max_retries=3)  
    def search_content(self, *args, **kwargs):
        return super().search_content(*args, **kwargs)
    
    def safe_search_by_company(self, company_name: str, **kwargs):
        """Search with comprehensive error handling"""
        try:
            result = self.search_by_company(company_name, **kwargs)
            
            if result["isError"]:
                print(f"Server error for {company_name}: {result['errorMessage']}")
                return None
            
            return result
            
        except requests.ConnectionError:
            print(f"Connection error when searching for {company_name}")
            return None
        except requests.Timeout:
            print(f"Timeout when searching for {company_name}")
            return None
        except Exception as e:
            print(f"Unexpected error for {company_name}: {e}")
            return None

# Usage with error handling
client = RobustSECEdgarMCPClient()

companies = ["Apple Inc.", "Microsoft Corporation", "Invalid Company Name"]
for company in companies:
    result = client.safe_search_by_company(company, form_types=["10-K"])
    if result:
        total = result["content"]["totalCount"]
        print(f"{company}: Found {total} documents")
    else:
        print(f"{company}: No results or error occurred")
```

## Testing

```python
import unittest
from unittest.mock import Mock, patch

class TestSECEdgarMCPClient(unittest.TestCase):
    def setUp(self):
        self.client = SECEdgarMCPClient("http://test-server")
    
    @patch('requests.Session.post')
    def test_company_search(self, mock_post):
        # Mock response
        mock_response = Mock()
        mock_response.json.return_value = {
            "content": {"totalCount": 5, "items": []},
            "isError": False
        }
        mock_response.raise_for_status.return_value = None
        mock_post.return_value = mock_response
        
        # Test
        result = self.client.search_by_company("Apple Inc.")
        
        # Assertions
        self.assertFalse(result["isError"])
        self.assertEqual(result["content"]["totalCount"], 5)
        mock_post.assert_called_once()

if __name__ == "__main__":
    unittest.main()
```

## Configuration

Create a configuration file for different environments:

```python
# config.py
import os

class Config:
    MCP_SERVER_URL = os.getenv("MCP_SERVER_URL", "http://localhost:5236")
    REQUEST_TIMEOUT = int(os.getenv("REQUEST_TIMEOUT", "30"))
    MAX_RETRIES = int(os.getenv("MAX_RETRIES", "3"))
    PAGE_SIZE_DEFAULT = int(os.getenv("PAGE_SIZE_DEFAULT", "50"))

class DevelopmentConfig(Config):
    MCP_SERVER_URL = "http://localhost:5236"

class ProductionConfig(Config):
    MCP_SERVER_URL = os.getenv("MCP_SERVER_URL", "https://your-production-server.com")
    REQUEST_TIMEOUT = 60

# Use in client
config = DevelopmentConfig()  # or ProductionConfig()
client = SECEdgarMCPClient(config.MCP_SERVER_URL)
```

This comprehensive Python client provides robust integration patterns, error handling, and real-world usage examples for the SEC Edgar MCP server.