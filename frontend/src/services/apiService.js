import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || '';

// Service to fetch company tickers from SEC via our backend
export const fetchCompanyTickers = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/companies`);
    
    // The response is an object with numeric keys, we need to convert it to an array
    const companiesObject = response.data;
    const companiesArray = Object.values(companiesObject).map(company => ({
      cik: company.cik_str,
      ticker: company.ticker,
      title: company.title
    }));
    
    return companiesArray;
  } catch (error) {
    console.error('Error fetching company tickers:', error);
    throw new Error('Failed to fetch company data from SEC');
  }
};

// Service to fetch previously crawled companies
export const fetchCrawledCompanies = async (connectionId = null) => {
  try {
    const url = connectionId 
      ? `${API_BASE_URL}/crawled-companies?connectionId=${encodeURIComponent(connectionId)}`
      : `${API_BASE_URL}/crawled-companies`;
    
    console.log('API Service: Fetching crawled companies for connection:', connectionId);
    const response = await axios.get(url);
    return response.data;
  } catch (error) {
    console.error('Error fetching crawled companies:', error);
    // Don't throw error - this is optional data
    return {
      lastCrawlDate: null,
      companies: [],
      totalCompanies: 0,
      connectionId: connectionId
    };
  }
};

// Service to trigger the crawl process
export const triggerCrawl = async (selectedCompanies, connectionId = null) => {
  try {
    console.log('API Service: Triggering crawl for companies:', selectedCompanies);
    console.log('API Service: connectionId parameter:', connectionId);
    
    // Use the new endpoint if connectionId is provided
    const endpoint = connectionId ? '/loadcontent-to-connection' : '/loadcontent';
    const payload = connectionId 
      ? { companies: selectedCompanies, connectionId: connectionId }
      : { companies: selectedCompanies };
    
    console.log('API Service: Using endpoint:', endpoint);
    console.log('API Service: Payload:', JSON.stringify(payload, null, 2));
      
    const response = await axios.post(`${API_BASE_URL}${endpoint}`, payload);
    
    console.log('Crawl response:', response.data);
    return response.data;
  } catch (error) {
    console.error('Error triggering crawl:', error);
    throw new Error('Failed to trigger crawl process');
  }
};

// Service to trigger recrawl of all previously crawled companies
export const triggerRecrawlAll = async (connectionId = null) => {
  try {
    console.log('API Service: Starting triggerRecrawlAll request');
    console.log('API Service: connectionId parameter:', connectionId);
    console.log('API Service: Making POST request to:', `${API_BASE_URL}/recrawl-all`);
    
    // Send connectionId in the request body if provided
    const payload = connectionId ? { connectionId } : {};
    console.log('API Service: Recrawl payload:', JSON.stringify(payload, null, 2));
    
    const response = await axios.post(`${API_BASE_URL}/recrawl-all`, payload);
    
    console.log('API Service: Recrawl response received:', response.data);
    console.log('API Service: Response status:', response.status);
    return response.data;
  } catch (error) {
    console.error('API Service: Error triggering recrawl:', error);
    console.error('API Service: Error response:', error.response);
    if (error.response?.status === 400) {
      throw new Error('No previously crawled companies found. Please crawl companies first.');
    }
    throw new Error('Failed to trigger recrawl process');
  }
};

// Service to provision connection
export const provisionConnection = async (tenantId) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/provisionconnection`, tenantId, {
      headers: {
        'Content-Type': 'text/plain'
      }
    });
    
    return response.data;
  } catch (error) {
    console.error('Error provisioning connection:', error);
    throw new Error('Failed to provision connection');
  }
};

// Service to check crawl status (if needed)
export const getCrawlStatus = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/crawl-status`);
    return response.data;
  } catch (error) {
    console.error('Error getting crawl status:', error);
    throw new Error('Failed to get crawl status');
  }
};

// Storage Configuration Services
export const getStorageConfig = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/storage-config`);
    return response.data;
  } catch (error) {
    console.error('Error getting storage configuration:', error);
    throw new Error('Failed to get storage configuration');
  }
};

export const saveStorageConfig = async (config) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/storage-config`, config);
    return response.data;
  } catch (error) {
    console.error('Error saving storage configuration:', error);
    throw new Error('Failed to save storage configuration');
  }
};

export const testStorageConfig = async (config) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/storage-config/test`, config);
    return response.data;
  } catch (error) {
    console.error('Error testing storage configuration:', error);
    throw new Error('Failed to test storage configuration');
  }
};

// New methods for crawl metrics
export const getCrawlMetrics = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/crawl-metrics`);
    return response.data;
  } catch (error) {
    console.error('Error fetching crawl metrics:', error);
    throw new Error('Failed to fetch crawl metrics');
  }
};

export const getCompanyCrawlMetrics = async (companyName) => {
  try {
    const response = await axios.get(`${API_BASE_URL}/crawl-metrics/${encodeURIComponent(companyName)}`);
    return response.data;
  } catch (error) {
    console.error('Error fetching company crawl metrics:', error);
    throw new Error('Failed to fetch company crawl metrics');
  }
};

export const getCrawlErrors = async (companyName = null) => {
  try {
    const url = companyName 
      ? `${API_BASE_URL}/crawl-errors?company=${encodeURIComponent(companyName)}`
      : `${API_BASE_URL}/crawl-errors`;
    const response = await axios.get(url);
    return response.data;
  } catch (error) {
    console.error('Error fetching crawl errors:', error);
    throw new Error('Failed to fetch crawl errors');
  }
};

// Data Collection Configuration methods
export const getDataCollectionConfig = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/data-collection-config`);
    return response.data;
  } catch (error) {
    console.error('Error fetching data collection config:', error);
    throw new Error('Failed to fetch data collection config');
  }
};

export const saveDataCollectionConfig = async (config) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/data-collection-config`, config);
    return response.data;
  } catch (error) {
    console.error('Error saving data collection config:', error);
    throw new Error('Failed to save data collection config');
  }
};

// Yearly metrics methods
export const getYearlyMetrics = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/crawl-metrics/yearly`);
    return response.data;
  } catch (error) {
    console.error('Error fetching yearly metrics:', error);
    throw new Error('Failed to fetch yearly metrics');
  }
};

export const getCompanyYearlyMetrics = async (companyName) => {
  try {
    const response = await axios.get(`${API_BASE_URL}/crawl-metrics/yearly/${encodeURIComponent(companyName)}`);
    return response.data;
  } catch (error) {
    console.error('Error fetching company yearly metrics:', error);
    throw new Error('Failed to fetch company yearly metrics');
  }
};

// Service to get scheduler configuration
export const getSchedulerConfig = async () => {
  try {
    console.log('API Service: Getting scheduler config');
    const response = await axios.get(`${API_BASE_URL}/scheduler-config`);
    console.log('API Service: Scheduler config response:', response.data);
    return response.data;
  } catch (error) {
    console.error('API Service: Error getting scheduler config:', error);
    throw new Error('Failed to get scheduler configuration');
  }
};

// Service to save scheduler configuration
export const saveSchedulerConfig = async (config) => {
  try {
    console.log('API Service: Saving scheduler config:', config);
    const response = await axios.post(`${API_BASE_URL}/scheduler-config`, config);
    console.log('API Service: Scheduler config saved:', response.data);
    return response.data;
  } catch (error) {
    console.error('API Service: Error saving scheduler config:', error);
    throw new Error('Failed to save scheduler configuration');
  }
};

// External Connection Management Services
export const fetchExternalConnections = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/external-connections`);
    return response.data;
  } catch (error) {
    console.error('Error fetching external connections:', error);
    throw new Error('Failed to fetch external connections');
  }
};

export const createExternalConnection = async (connectionData) => {
  try {
    const response = await axios.post(`${API_BASE_URL}/external-connections`, connectionData);
    return response.data;
  } catch (error) {
    console.error('Error creating external connection:', error);
    throw new Error(error.response?.data || 'Failed to create external connection');
  }
};

export const deleteExternalConnection = async (connectionId) => {
  try {
    await axios.delete(`${API_BASE_URL}/external-connections/${connectionId}`);
  } catch (error) {
    console.error('Error deleting external connection:', error);
    throw new Error(error.response?.data || 'Failed to delete external connection');
  }
};

// Export as default object for easier importing
const apiService = {
  get: axios.get,
  post: axios.post,
  fetchCompanyTickers,
  fetchCrawledCompanies,
  triggerCrawl,
  provisionConnection,
  getCrawlStatus,
  getStorageConfig,
  saveStorageConfig,
  testStorageConfig,
  getCrawlMetrics,
  getCompanyCrawlMetrics,
  getCrawlErrors,
  getDataCollectionConfig,
  saveDataCollectionConfig,
  getYearlyMetrics,
  getCompanyYearlyMetrics,
  getSchedulerConfig,
  saveSchedulerConfig
};

export { apiService };
export default apiService;
