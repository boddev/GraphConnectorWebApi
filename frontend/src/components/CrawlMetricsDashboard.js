import React, { useState, useEffect } from 'react';
import apiService from '../services/apiService';
import './CrawlMetricsDashboard.css';

const CrawlMetricsDashboard = () => {
  const [overallMetrics, setOverallMetrics] = useState(null);
  const [crawlStatus, setCrawlStatus] = useState(null);
  const [processingErrors, setProcessingErrors] = useState([]);
  const [selectedCompany, setSelectedCompany] = useState('');
  const [companyMetrics, setCompanyMetrics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      const [metricsResponse, statusResponse, errorsResponse] = await Promise.all([
        apiService.get('/crawl-metrics'),
        apiService.get('/crawl-status'),
        apiService.get('/crawl-errors')
      ]);

      setOverallMetrics(metricsResponse.data);
      setCrawlStatus(statusResponse.data);
      setProcessingErrors(errorsResponse.data);
      setError('');
    } catch (err) {
      console.error('Error fetching crawl data:', err);
      setError('Failed to fetch crawl metrics');
    } finally {
      setLoading(false);
    }
  };

  const fetchCompanyMetrics = async (companyName) => {
    if (!companyName) {
      setCompanyMetrics(null);
      return;
    }

    try {
      const response = await apiService.get(`/crawl-metrics/${encodeURIComponent(companyName)}`);
      setCompanyMetrics(response.data);
    } catch (err) {
      console.error('Error fetching company metrics:', err);
      setCompanyMetrics(null);
    }
  };

  const handleCompanyChange = (e) => {
    const company = e.target.value;
    setSelectedCompany(company);
    fetchCompanyMetrics(company);
  };

  const formatDate = (dateString) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleString();
  };

  const formatSuccessRate = (rate) => {
    return `${rate.toFixed(1)}%`;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-lg">Loading crawl metrics...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4">
        {error}
        <button 
          onClick={fetchData}
          className="ml-4 bg-red-600 text-white px-3 py-1 rounded text-sm hover:bg-red-700"
        >
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="dashboard-header">
        <h2 className="dashboard-title">Document Crawl Metrics</h2>
        <button 
          onClick={fetchData}
          className="refresh-button"
        >
          Refresh
        </button>
      </div>

      {/* Overall Status Card */}
      {crawlStatus && (
        <div className="metric-card">
          <h3 className="section-title">Crawl Status</h3>
          <div className="metric-grid">
            <div className="metric-item">
              <div className="metric-number blue">{crawlStatus.totalDocuments}</div>
              <div className="metric-label">Total Documents</div>
            </div>
            <div className="metric-item">
              <div className="metric-number green">{crawlStatus.successfulDocuments}</div>
              <div className="metric-label">Successful</div>
            </div>
            <div className="metric-item">
              <div className="metric-number red">{crawlStatus.failedDocuments}</div>
              <div className="metric-label">Failed</div>
            </div>
            <div className="metric-item">
              <div className="metric-number yellow">{crawlStatus.pendingDocuments}</div>
              <div className="metric-label">Pending</div>
            </div>
          </div>
          
          <div className="info-grid">
            <div className="info-item">
              <span className="info-label">Success Rate:</span> 
              <span className={`info-value ${crawlStatus.successRate >= 90 ? 'success' : crawlStatus.successRate >= 70 ? 'warning' : 'error'}`}>
                {formatSuccessRate(crawlStatus.successRate)}
              </span>
            </div>
            <div className="info-item">
              <span className="info-label">Storage:</span> 
              <span className="info-value">{crawlStatus.storageType}</span>
            </div>
            <div className="info-item">
              <span className="info-label">Last Processed:</span> 
              <span className="info-value">{formatDate(crawlStatus.lastProcessedDate)}</span>
            </div>
          </div>
        </div>
      )}

      {/* Overall Metrics */}
      {overallMetrics && (
        <div className="metric-card">
          <h3 className="section-title">Overall Metrics</h3>
          <div className="metric-grid">
            <div className="metric-item">
              <div className="metric-number blue">{overallMetrics.totalCompanies}</div>
              <div className="metric-label">Total Companies</div>
            </div>
            <div className="metric-item">
              <div className="metric-label">Last Crawl</div>
              <div className="info-value" style={{fontSize: '0.875rem', fontWeight: '500'}}>{formatDate(overallMetrics.lastCrawlDate)}</div>
            </div>
            <div className="metric-item">
              <div className="metric-number green">{formatSuccessRate(overallMetrics.overallSuccessRate)}</div>
              <div className="metric-label">Overall Success Rate</div>
            </div>
          </div>

          {/* Form Type Distribution */}
          {overallMetrics.formTypeCounts && Object.keys(overallMetrics.formTypeCounts).length > 0 && (
            <div style={{marginTop: '24px'}}>
              <h4 className="section-title" style={{fontSize: '1rem', marginBottom: '12px'}}>Document Types</h4>
              <div className="form-type-grid">
                {Object.entries(overallMetrics.formTypeCounts).map(([form, count]) => (
                  <div key={form} className="form-type-item">
                    <div className="form-type-name">{form}</div>
                    <div className="form-type-count">{count}</div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Company Metrics */}
      <div className="metric-card">
        <h3 className="section-title">Company-Specific Metrics</h3>
        
        <div style={{marginBottom: '16px'}}>
          <label className="select-label">
            Select Company:
          </label>
          <select 
            value={selectedCompany} 
            onChange={handleCompanyChange}
            className="select-input"
          >
            <option value="">Select a company...</option>
            {overallMetrics?.companyMetrics?.map((company) => (
              <option key={company.companyName} value={company.companyName}>
                {company.companyName} ({company.totalDocuments} docs)
              </option>
            ))}
          </select>
        </div>

        {companyMetrics && (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-gray-50 p-4 rounded">
              <div className="text-sm text-gray-600">Total Documents</div>
              <div className="text-xl font-bold">{companyMetrics.totalDocuments}</div>
            </div>
            <div className="bg-green-50 p-4 rounded">
              <div className="text-sm text-gray-600">Successful</div>
              <div className="text-xl font-bold text-green-600">{companyMetrics.successfulDocuments}</div>
            </div>
            <div className="bg-red-50 p-4 rounded">
              <div className="text-sm text-gray-600">Failed</div>
              <div className="text-xl font-bold text-red-600">{companyMetrics.failedDocuments}</div>
            </div>
            <div className="bg-yellow-50 p-4 rounded">
              <div className="text-sm text-gray-600">Pending</div>
              <div className="text-xl font-bold text-yellow-600">{companyMetrics.pendingDocuments}</div>
            </div>
          </div>
        )}
      </div>

      {/* Processing Errors */}
      {processingErrors.length > 0 && (
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-semibold mb-4 text-red-800">Recent Processing Errors</h3>
          <div className="space-y-3 max-h-80 overflow-y-auto">
            {processingErrors.slice(0, 10).map((error, index) => (
              <div key={index} className="border-l-4 border-red-400 bg-red-50 p-3">
                <div className="flex justify-between items-start">
                  <div className="flex-1">
                    <div className="font-medium text-red-800">{error.companyName}</div>
                    <div className="text-sm text-red-600">{error.form} - {formatDate(error.errorDate)}</div>
                    <div className="text-sm text-gray-700 mt-1">{error.errorMessage}</div>
                  </div>
                  <a 
                    href={error.url} 
                    target="_blank" 
                    rel="noopener noreferrer"
                    className="text-blue-600 hover:text-blue-800 text-sm"
                  >
                    View Document
                  </a>
                </div>
              </div>
            ))}
          </div>
          {processingErrors.length > 10 && (
            <div className="text-center mt-4 text-sm text-gray-600">
              Showing 10 of {processingErrors.length} errors
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default CrawlMetricsDashboard;
