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
export const fetchCrawledCompanies = async () => {
  try {
    const response = await axios.get(`${API_BASE_URL}/crawled-companies`);
    return response.data;
  } catch (error) {
    console.error('Error fetching crawled companies:', error);
    // Don't throw error - this is optional data
    return {
      lastCrawlDate: null,
      companies: [],
      totalCompanies: 0
    };
  }
};

// Service to trigger the crawl process
export const triggerCrawl = async (selectedCompanies) => {
  try {
    console.log('Triggering crawl for companies:', selectedCompanies);
    const response = await axios.post(`${API_BASE_URL}/loadcontent`, {
      companies: selectedCompanies
    });
    
    console.log('Crawl response:', response.data);
    return response.data;
  } catch (error) {
    console.error('Error triggering crawl:', error);
    throw new Error('Failed to trigger crawl process');
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
    const response = await axios.get(`${API_BASE_URL}/status`);
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

// Export as default object for easier importing
export const apiService = {
  fetchCompanyTickers,
  fetchCrawledCompanies,
  triggerCrawl,
  provisionConnection,
  getCrawlStatus,
  getStorageConfig,
  saveStorageConfig,
  testStorageConfig
};
