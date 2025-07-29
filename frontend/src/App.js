import React, { useState, useEffect } from 'react';
import CompanySelector from './components/CompanySelector';
import CrawlControls from './components/CrawlControls';
import CrawlHistory from './components/CrawlHistory';
import { fetchCompanyTickers, fetchCrawledCompanies, triggerCrawl } from './services/apiService';

function App() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanies, setSelectedCompanies] = useState([]);
  const [crawledHistory, setCrawledHistory] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [crawling, setCrawling] = useState(false);

  // Load company data and crawl history on component mount
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoading(true);
        setError('');
        
        // Load both companies and crawl history in parallel
        const [companiesData, crawlHistoryData] = await Promise.all([
          fetchCompanyTickers(),
          fetchCrawledCompanies()
        ]);
        
        // Sort companies by ticker for better UX
        const sortedCompanies = companiesData.sort((a, b) => a.ticker.localeCompare(b.ticker));
        setCompanies(sortedCompanies);
        setCrawledHistory(crawlHistoryData);
        
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, []);

  const handleSelectionChange = (newSelection) => {
    setSelectedCompanies(newSelection);
  };

  const handleTriggerCrawl = async (companies) => {
    setCrawling(true);
    try {
      await triggerCrawl(companies);
      // Refresh crawl history after successful crawl
      const updatedHistory = await fetchCrawledCompanies();
      setCrawledHistory(updatedHistory);
    } finally {
      setCrawling(false);
    }
  };

  return (
    <div className="app-container">
      <header className="app-header">
        <h1>Edgar SEC Filings Connector</h1>
        <p>Select companies to crawl their SEC filings for Microsoft Graph integration</p>
      </header>
      
      <main className="main-content">
        <CompanySelector
          companies={companies}
          selectedCompanies={selectedCompanies}
          onSelectionChange={handleSelectionChange}
          loading={loading}
          error={error}
        />
        
        <CrawlControls
          selectedCompanies={selectedCompanies}
          onTriggerCrawl={handleTriggerCrawl}
          crawling={crawling}
        />

        <CrawlHistory 
          crawledHistory={crawledHistory}
        />
      </main>
    </div>
  );
}

export default App;
