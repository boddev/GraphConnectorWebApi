import React from 'react';

const CrawlHistory = ({ crawledHistory }) => {
  if (!crawledHistory || 
      (!crawledHistory.companies && !crawledHistory.Companies) || 
      (crawledHistory.companies && crawledHistory.companies.length === 0) ||
      (crawledHistory.Companies && crawledHistory.Companies.length === 0)) {
    return (
      <div className="section">
        <h2>Previous Crawls</h2>
        <div className="no-history">
          No companies have been crawled yet. Select companies above and start your first crawl!
        </div>
      </div>
    );
  }

  // Use the appropriate property name (support both camelCase and PascalCase)
  const companies = crawledHistory.companies || crawledHistory.Companies || [];

  const formatDate = (dateString) => {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    return isNaN(date.getTime()) ? 'Invalid Date' : date.toLocaleString();
  };

  return (
    <div className="section">
      <h2>Previous Crawls</h2>
      
      <div className="crawl-history-summary">
        <div className="history-stat">
          <div className="history-stat-number">{crawledHistory.totalCompanies || crawledHistory.TotalCompanies || 0}</div>
          <div className="history-stat-label">Companies Crawled</div>
        </div>
        <div className="history-stat">
          <div className="history-stat-date">{formatDate(crawledHistory.lastCrawlDate || crawledHistory.LastCrawlDate)}</div>
          <div className="history-stat-label">Last Crawl</div>
        </div>
      </div>

      <div className="crawled-companies-section">
        <h3>Previously Crawled Companies</h3>
        <div className="crawled-companies-grid">
          {companies.map((company) => {
            // Handle both uppercase (C# serialization) and lowercase property names
            const cik = company.cik || company.Cik;
            const ticker = company.ticker || company.Ticker;
            const title = company.title || company.Title;
            
            return (
              <div 
                key={`crawled-${cik}-${ticker}`} 
                className="crawled-company-item"
              >
                <div className="crawled-company-info">
                  <div className="crawled-company-name">{title}</div>
                  <div className="crawled-company-ticker">
                    {ticker} (CIK: {cik ? cik.toString().padStart(10, '0') : 'N/A'})
                  </div>
                </div>
                <div className="crawled-company-status">
                  ✓ Crawled
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export default CrawlHistory;
