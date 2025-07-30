import React, { useState, useEffect } from 'react';
import apiService from './apiService';

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
      <div className="flex justify-between items-center">
        <h2 className="text-2xl font-bold text-gray-900">Document Crawl Metrics</h2>
        <button 
          onClick={fetchData}
          className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
        >
          Refresh
        </button>
      </div>

      {/* Overall Status Card */}
      {crawlStatus && (
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-semibold mb-4">Crawl Status</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="text-center">
              <div className="text-2xl font-bold text-blue-600">{crawlStatus.totalDocuments}</div>
              <div className="text-sm text-gray-600">Total Documents</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-green-600">{crawlStatus.successfulDocuments}</div>
              <div className="text-sm text-gray-600">Successful</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-red-600">{crawlStatus.failedDocuments}</div>
              <div className="text-sm text-gray-600">Failed</div>
            </div>
            <div className="text-center">
              <div className="text-2xl font-bold text-yellow-600">{crawlStatus.pendingDocuments}</div>
              <div className="text-sm text-gray-600">Pending</div>
            </div>
          </div>
          
          <div className="mt-4 grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
            <div>
              <span className="font-medium">Success Rate:</span> 
              <span className={`ml-2 ${crawlStatus.successRate >= 90 ? 'text-green-600' : crawlStatus.successRate >= 70 ? 'text-yellow-600' : 'text-red-600'}`}>
                {formatSuccessRate(crawlStatus.successRate)}
              </span>
            </div>
            <div>
              <span className="font-medium">Storage:</span> 
              <span className="ml-2 text-gray-700">{crawlStatus.storageType}</span>
            </div>
            <div>
              <span className="font-medium">Last Processed:</span> 
              <span className="ml-2 text-gray-700">{formatDate(crawlStatus.lastProcessedDate)}</span>
            </div>
          </div>
        </div>
      )}

      {/* Overall Metrics */}
      {overallMetrics && (
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-semibold mb-4">Overall Metrics</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div className="bg-gray-50 p-4 rounded">
              <div className="text-sm text-gray-600">Total Companies</div>
              <div className="text-xl font-bold">{overallMetrics.totalCompanies}</div>
            </div>
            <div className="bg-gray-50 p-4 rounded">
              <div className="text-sm text-gray-600">Last Crawl</div>
              <div className="text-sm font-medium">{formatDate(overallMetrics.lastCrawlDate)}</div>
            </div>
            <div className="bg-gray-50 p-4 rounded">
              <div className="text-sm text-gray-600">Overall Success Rate</div>
              <div className="text-xl font-bold">{formatSuccessRate(overallMetrics.overallSuccessRate)}</div>
            </div>
          </div>

          {/* Form Type Distribution */}
          {overallMetrics.formTypeCounts && Object.keys(overallMetrics.formTypeCounts).length > 0 && (
            <div className="mt-6">
              <h4 className="text-md font-medium mb-3">Document Types</h4>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
                {Object.entries(overallMetrics.formTypeCounts).map(([form, count]) => (
                  <div key={form} className="bg-blue-50 p-2 rounded text-center">
                    <div className="font-medium text-blue-800">{form}</div>
                    <div className="text-blue-600">{count}</div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Company Metrics */}
      <div className="bg-white rounded-lg shadow p-6">
        <h3 className="text-lg font-semibold mb-4">Company-Specific Metrics</h3>
        
        <div className="mb-4">
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Select Company:
          </label>
          <select 
            value={selectedCompany} 
            onChange={handleCompanyChange}
            className="border border-gray-300 rounded-md px-3 py-2 w-full max-w-md"
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
