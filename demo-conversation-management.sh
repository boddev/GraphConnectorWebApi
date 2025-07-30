#!/bin/bash

# Conversation Management Demo Script
# This script demonstrates the full conversation management workflow
# integrated with MCP document search tools

set -e

BASE_URL="http://localhost:5236"

echo "🚀 Starting Conversation Management Demo"
echo "========================================"

# 1. Create a conversation session
echo "📱 Creating conversation session..."
SESSION_RESPONSE=$(curl -s -X POST "$BASE_URL/conversations/sessions" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "financial-analyst",
    "ttlHours": 4
  }')

SESSION_ID=$(echo $SESSION_RESPONSE | jq -r '.id')
echo "✅ Session created: $SESSION_ID"

# 2. Create a conversation for SEC filing analysis
echo "💬 Creating conversation for SEC filing analysis..."
CONV_RESPONSE=$(curl -s -X POST "$BASE_URL/conversations/sessions/$SESSION_ID/conversations" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Apple SEC Filings Analysis"
  }')

CONV_ID=$(echo $CONV_RESPONSE | jq -r '.id')
echo "✅ Conversation created: $CONV_ID"

# 3. User asks a question
echo "❓ User asks about Apple 10-K filings..."
curl -s -X POST "$BASE_URL/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 0,
    "content": "Can you find Apple latest 10-K filing and tell me about their revenue trends?"
  }' > /dev/null

echo "✅ User message added"

# 4. Simulate MCP tool call for document search
echo "🔍 Simulating MCP document search tool call..."
curl -s -X POST "$BASE_URL/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 3,
    "content": "Searching for Apple 10-K filings...",
    "toolName": "search_documents_by_company",
    "toolCallId": "tool-call-001",
    "metadata": {
      "searchParameters": {
        "companyName": "Apple",
        "formTypes": ["10-K"],
        "includeContent": false
      }
    }
  }' > /dev/null

echo "✅ Tool call message added"

# 5. Assistant responds with search results and citations
echo "🤖 Assistant responds with findings and citations..."
curl -s -X POST "$BASE_URL/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 1,
    "content": "I found Apple Inc. 2023 Form 10-K filing. Based on the analysis, Apple revenue shows consistent growth with total net sales of $383.29 billion in fiscal 2023, representing a slight decline from $394.33 billion in 2022. The company attributes this to challenging macroeconomic conditions but maintains strong fundamentals across iPhone, Mac, iPad, and Services segments.",
    "citations": [
      {
        "documentId": "aapl-10k-20230930",
        "documentTitle": "Apple Inc. Form 10-K Annual Report (Fiscal Year 2023)",
        "companyName": "Apple Inc.",
        "formType": "10-K",
        "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019323000077/aapl-20230930.htm",
        "filingDate": "2023-11-03T00:00:00Z",
        "relevantExcerpt": "Total net sales decreased 3% or $11.0 billion during 2023 compared to 2022. The decrease was driven by lower sales of iPhone, Mac and iPad, partially offset by higher sales of Services.",
        "relevanceScore": 0.95
      }
    ],
    "metadata": {
      "searchQuery": "Apple 10-K",
      "documentsFound": 1,
      "analysisType": "revenue_trends",
      "responseTime": "2.3s"
    }
  }' > /dev/null

echo "✅ Assistant response with citations added"

# 6. Update conversation context with search information
echo "🔧 Updating conversation context..."
curl -s -X PUT "$BASE_URL/conversations/$CONV_ID/context" \
  -H "Content-Type: application/json" \
  -d '{
    "currentCompany": "Apple Inc.",
    "focusArea": "revenue_analysis",
    "documentsAnalyzed": ["aapl-10k-20230930"],
    "lastSearchTimestamp": "2025-07-30T22:45:00Z",
    "userIntent": "financial_analysis",
    "conversationTopic": "SEC_filing_analysis",
    "searchFilters": {
      "companyName": "Apple",
      "formTypes": ["10-K"],
      "dateRange": "2023"
    }
  }' > /dev/null

echo "✅ Context updated"

# 7. User follows up with another question
echo "❓ User asks follow-up question..."
curl -s -X POST "$BASE_URL/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 0,
    "content": "What about their risk factors? Any major changes from previous years?"
  }' > /dev/null

echo "✅ Follow-up question added"

# 8. Assistant responds using conversation context
echo "🤖 Assistant responds using context from previous interaction..."
curl -s -X POST "$BASE_URL/conversations/$CONV_ID/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "role": 1,
    "content": "Based on Apple 2023 10-K filing I previously analyzed, the key risk factors include supply chain dependencies, competitive market conditions, and regulatory changes. Notable additions include increased focus on geopolitical tensions affecting operations in China and supply chain diversification challenges.",
    "citations": [
      {
        "documentId": "aapl-10k-20230930",
        "documentTitle": "Apple Inc. Form 10-K Annual Report (Fiscal Year 2023)",
        "companyName": "Apple Inc.",
        "formType": "10-K",
        "url": "https://www.sec.gov/Archives/edgar/data/320193/000032019323000077/aapl-20230930.htm",
        "filingDate": "2023-11-03T00:00:00Z",
        "relevantExcerpt": "The Company is subject to various risks related to its international operations, including... geopolitical tensions and conflicts.",
        "relevanceScore": 0.88
      }
    ],
    "metadata": {
      "contextUsed": true,
      "previousDocuments": ["aapl-10k-20230930"],
      "analysisType": "risk_factors"
    }
  }' > /dev/null

echo "✅ Context-aware response added"

# 9. Display the complete conversation
echo "📋 Retrieving complete conversation..."
FULL_CONVERSATION=$(curl -s -X GET "$BASE_URL/conversations/$CONV_ID")
echo "✅ Conversation retrieved"

echo ""
echo "📊 Conversation Summary"
echo "====================="
echo "Session ID: $SESSION_ID"
echo "Conversation ID: $CONV_ID"
echo "Messages Count: $(echo $FULL_CONVERSATION | jq '.messages | length')"
echo "Title: $(echo $FULL_CONVERSATION | jq -r '.conversation.title')"

echo ""
echo "💬 Message History:"
echo $FULL_CONVERSATION | jq -r '.messages[] | "[\(.timestamp)] \(.role | if . == 0 then "USER" elif . == 1 then "ASSISTANT" elif . == 2 then "SYSTEM" else "TOOL" end): \(.content | .[0:100])..."'

echo ""
echo "🔗 Citations Found:"
echo $FULL_CONVERSATION | jq -r '.messages[] | select(.citations != null) | .citations[] | "- \(.documentTitle) (\(.formType)) - Score: \(.relevanceScore)"'

echo ""
echo "🎯 Conversation Context:"
echo $FULL_CONVERSATION | jq '.conversation.context'

# 10. Get session conversations list
echo ""
echo "📁 All conversations in this session:"
SESSION_CONVERSATIONS=$(curl -s -X GET "$BASE_URL/conversations/sessions/$SESSION_ID/conversations")
echo $SESSION_CONVERSATIONS | jq -r '.[] | "- \(.title) (\(.id)) - Messages: \(.messages | length), Last: \(.lastMessageAt)"'

# 11. Display system metrics
echo ""
echo "📈 System Metrics:"
METRICS=$(curl -s -X GET "$BASE_URL/conversations/metrics")
echo $METRICS | jq '.'

echo ""
echo "✨ Demo completed successfully!"
echo "🔍 The conversation system demonstrates:"
echo "   • Multi-turn conversation management"
echo "   • Context preservation across messages"
echo "   • Integration with MCP document search tools"
echo "   • Citation and metadata handling"
echo "   • Session-based isolation"
echo "   • Real-time conversation updates"
echo ""
echo "🚀 Ready for M365 Copilot integration!"