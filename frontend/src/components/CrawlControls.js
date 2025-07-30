import React, { useState } from 'react';

const CrawlControls = ({ selectedCompanies, onTriggerCrawl, onTriggerRecrawlAll, crawling, crawledHistory }) => {
  const [message, setMessage] = useState('');
  const [messageType, setMessageType] = useState(''); // 'success' or 'error'

  const handleCrawlClick = async () => {
    if (selectedCompanies.length === 0) {
      setMessage('Please select at least one company before starting the crawl.');
      setMessageType('error');
      return;
    }

    try {
      setMessage('');
      await onTriggerCrawl(selectedCompanies);
      setMessage(`Crawl started successfully for ${selectedCompanies.length} companies. The process is running in the background.`);
      setMessageType('success');
    } catch (error) {
      setMessage(`Failed to start crawl: ${error.message}`);
      setMessageType('error');
    }
  };

  const handleRecrawlAllClick = async () => {
    console.log('Recrawl button clicked');
    console.log('Crawled history:', crawledHistory);
    
    if (!crawledHistory?.companies?.length) {
      console.log('No previously crawled companies found');
      setMessage('No previously crawled companies found. Please crawl companies first.');
      setMessageType('error');
      return;
    }

    console.log(`Found ${crawledHistory.companies.length} previously crawled companies`);

    try {
      setMessage('');
      console.log('Calling onTriggerRecrawlAll()');
      await onTriggerRecrawlAll();
      console.log('Recrawl completed successfully');
      setMessage(`Recrawl started successfully for ${crawledHistory.companies.length} previously crawled companies. The process is running in the background.`);
      setMessageType('success');
    } catch (error) {
      console.error('Error during recrawl:', error);
      setMessage(`Failed to start recrawl: ${error.message}`);
      setMessageType('error');
    }
  };

  return (
    <div className="section">
      <h2>Crawl Controls</h2>
      
      {message && (
        <div className={messageType}>
          {message}
        </div>
      )}

      <div className="actions-container">
        <button
          className="crawl-button"
          onClick={handleCrawlClick}
          disabled={crawling || selectedCompanies.length === 0}
        >
          {crawling ? 'Crawling in Progress...' : `Start Crawl (${selectedCompanies.length} companies)`}
        </button>
        
        {crawledHistory?.companies?.length > 0 && (
          <button
            className="recrawl-button"
            onClick={handleRecrawlAllClick}
            disabled={crawling}
            title={`Recrawl all ${crawledHistory.companies.length} previously crawled companies`}
          >
            {crawling ? 'Crawling in Progress...' : `Recrawl All (${crawledHistory.companies.length} companies)`}
          </button>
        )}
      </div>

      {selectedCompanies.length > 0 && !crawledHistory?.companies?.length && (
        <div className="section">
          <h3>Selected Companies for Crawl:</h3>
          <div style={{ maxHeight: '200px', overflowY: 'auto', border: '1px solid #ddd', borderRadius: '4px' }}>
            {selectedCompanies.map((company, index) => (
              <div key={company.cik} style={{ 
                padding: '8px 12px', 
                borderBottom: index < selectedCompanies.length - 1 ? '1px solid #eee' : 'none',
                fontSize: '14px'
              }}>
                <strong>{company.ticker}</strong> - {company.title}
              </div>
            ))}
          </div>
        </div>
      )}

      {selectedCompanies.length > 0 && crawledHistory?.companies?.length > 0 && (
        <div className="section">
          <h3>Selected Companies for New Crawl ({selectedCompanies.length}):</h3>
          <div style={{ maxHeight: '120px', overflowY: 'auto', border: '1px solid #ddd', borderRadius: '4px' }}>
            {selectedCompanies.slice(0, 5).map((company, index) => (
              <div key={company.cik} style={{ 
                padding: '6px 12px', 
                borderBottom: index < Math.min(selectedCompanies.length, 5) - 1 ? '1px solid #eee' : 'none',
                fontSize: '13px'
              }}>
                <strong>{company.ticker}</strong> - {company.title}
              </div>
            ))}
            {selectedCompanies.length > 5 && (
              <div style={{ padding: '6px 12px', fontSize: '13px', fontStyle: 'italic', color: '#666' }}>
                ... and {selectedCompanies.length - 5} more companies
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default CrawlControls;
