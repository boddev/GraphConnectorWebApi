import React, { useState, useEffect } from 'react';
import CompanySelector from './components/CompanySelector';
import CrawlControls from './components/CrawlControls';
import CrawlHistory from './components/CrawlHistory';
import StorageConfig from './components/StorageConfig';
import { fetchCompanyTickers, fetchCrawledCompanies, triggerCrawl } from './services/apiService';

function App() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanies, setSelectedCompanies] = useState([]);
  const [crawledHistory, setCrawledHistory] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [crawling, setCrawling] = useState(false);
  const [showStorageConfig, setShowStorageConfig] = useState(false);

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
        <div className="header-content">
          <div className="header-text">
            <h1>Edgar SEC Filings Connector</h1>
            <p>Select companies to crawl their SEC filings for Microsoft Graph integration</p>
          </div>
          <button 
            className="storage-config-button"
            onClick={() => setShowStorageConfig(true)}
            title="Configure Storage Settings"
          >
            ⚙️ Storage Settings
          </button>
        </div>
      </header>
      
      {/* Application Description */}
      <section className="app-description">
        <div className="description-card">
          <h2>📋 What This Application Does</h2>
          <p>
            This application automatically downloads and processes SEC filings for your selected companies. 
            Once you select companies and start the crawl process, the system will:
          </p>
          <ul className="features-list">
            <li>
              <strong>10-Q Forms:</strong> Download quarterly financial reports that provide detailed financial information for each quarter
            </li>
            <li>
              <strong>10-K Forms:</strong> Download annual reports that give a comprehensive overview of the company's business and financial condition
            </li>
            <li>
              <strong>8-K Forms:</strong> Download current reports that announce major events or corporate changes
            </li>
            <li>
              <strong>DEF 14A Forms:</strong> Download proxy statements that contain information about executive compensation, board members, and shareholder voting matters
            </li>
          </ul>
          <p className="integration-note">
            📊 All downloaded filings are automatically processed and integrated with Microsoft Graph, 
            making the financial data searchable and accessible through your organization's tools.
          </p>
        </div>
      </section>
      
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

      {/* Storage Configuration Modal */}
      {showStorageConfig && (
        <StorageConfig 
          onClose={() => setShowStorageConfig(false)}
        />
      )}
    </div>
  );
}

export default App;
