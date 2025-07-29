import React from 'react';

const CrawlHistory = ({ crawledHistory }) => {
  if (!crawledHistory || !crawledHistory.companies || crawledHistory.companies.length === 0) {
    return (
      <div className="section">
        <h2>Previous Crawls</h2>
        <div className="no-history">
          No companies have been crawled yet. Select companies above and start your first crawl!
        </div>
      </div>
    );
  }

  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <div className="section">
      <h2>Previous Crawls</h2>
      
      <div className="crawl-history-summary">
        <div className="history-stat">
          <div className="history-stat-number">{crawledHistory.totalCompanies}</div>
          <div className="history-stat-label">Companies Crawled</div>
        </div>
        <div className="history-stat">
          <div className="history-stat-date">{formatDate(crawledHistory.lastCrawlDate)}</div>
          <div className="history-stat-label">Last Crawl</div>
        </div>
      </div>

      <div className="crawled-companies-section">
        <h3>Previously Crawled Companies</h3>
        <div className="crawled-companies-grid">
          {crawledHistory.companies.map((company) => (
            <div 
              key={`crawled-${company.cik}-${company.ticker}`} 
              className="crawled-company-item"
            >
              <div className="crawled-company-info">
                <div className="crawled-company-name">{company.title}</div>
                <div className="crawled-company-ticker">
                  {company.ticker} (CIK: {company.cik.toString().padStart(10, '0')})
                </div>
              </div>
              <div className="crawled-company-status">
                ✓ Crawled
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default CrawlHistory;
