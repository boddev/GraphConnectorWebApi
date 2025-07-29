import React, { useState, useEffect } from 'react';
import { apiService } from '../services/apiService';
import './StorageConfig.css';

const StorageConfig = ({ onClose }) => {
  const [config, setConfig] = useState({
    provider: 'Local',
    azureConnectionString: '',
    companyTableName: 'companies',
    processedTableName: 'processed',
    blobContainerName: 'filings',
    localDataPath: './data',
    autoCreateTables: true
  });
  
  const [isLoading, setIsLoading] = useState(false);
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState(null);
  const [saveMessage, setSaveMessage] = useState('');

  useEffect(() => {
    loadCurrentConfig();
  }, []);

  const loadCurrentConfig = async () => {
    try {
      setIsLoading(true);
      const currentConfig = await apiService.getStorageConfig();
      setConfig(currentConfig);
    } catch (error) {
      console.error('Failed to load storage configuration:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleInputChange = (field, value) => {
    setConfig(prev => ({
      ...prev,
      [field]: value
    }));
    setTestResult(null);
    setSaveMessage('');
  };

  const testConnection = async () => {
    try {
      setIsTesting(true);
      setTestResult(null);
      
      const result = await apiService.testStorageConfig(config);
      setTestResult(result);
    } catch (error) {
      setTestResult({
        healthy: false,
        message: `Test failed: ${error.message}`
      });
    } finally {
      setIsTesting(false);
    }
  };

  const saveConfiguration = async () => {
    try {
      setIsLoading(true);
      setSaveMessage('');
      
      await apiService.saveStorageConfig(config);
      setSaveMessage('Configuration saved successfully!');
      
      // Auto-close after successful save
      setTimeout(() => {
        onClose();
      }, 1500);
    } catch (error) {
      setSaveMessage(`Failed to save: ${error.message}`);
    } finally {
      setIsLoading(false);
    }
  };

  const getStorageDescription = (provider) => {
    switch (provider) {
      case 'Azure':
        return 'Uses Azure Table Storage for scalable, persistent tracking. Recommended for production use.';
      case 'Local':
        return 'Uses local JSON files for tracking. Perfect for development and single-user setups.';
      case 'Memory':
        return 'Uses in-memory storage. Data is lost when application restarts. Good for testing.';
      default:
        return '';
    }
  };

  if (isLoading && !config.provider) {
    return (
      <div className="storage-config-overlay">
        <div className="storage-config-modal">
          <div className="loading">Loading configuration...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="storage-config-overlay">
      <div className="storage-config-modal">
        <div className="storage-config-header">
          <h2>Storage Configuration</h2>
          <button className="close-button" onClick={onClose}>×</button>
        </div>

        <div className="storage-config-content">
          <div className="config-section">
            <label>Storage Provider</label>
            <select 
              value={config.provider} 
              onChange={(e) => handleInputChange('provider', e.target.value)}
            >
              <option value="Local">Local File Storage</option>
              <option value="Azure">Azure Table Storage</option>
              <option value="Memory">In-Memory Storage</option>
            </select>
            <div className="provider-description">
              {getStorageDescription(config.provider)}
            </div>
          </div>

          {config.provider === 'Azure' && (
            <>
              <div className="config-section">
                <label>Azure Connection String *</label>
                <input
                  type="password"
                  value={config.azureConnectionString}
                  onChange={(e) => handleInputChange('azureConnectionString', e.target.value)}
                  placeholder="DefaultEndpointsProtocol=https;AccountName=..."
                />
              </div>

              <div className="config-row">
                <div className="config-section">
                  <label>Company Table Name</label>
                  <input
                    type="text"
                    value={config.companyTableName}
                    onChange={(e) => handleInputChange('companyTableName', e.target.value)}
                  />
                </div>
                <div className="config-section">
                  <label>Processed Table Name</label>
                  <input
                    type="text"
                    value={config.processedTableName}
                    onChange={(e) => handleInputChange('processedTableName', e.target.value)}
                  />
                </div>
              </div>

              <div className="config-row">
                <div className="config-section">
                  <label>Blob Container Name</label>
                  <input
                    type="text"
                    value={config.blobContainerName}
                    onChange={(e) => handleInputChange('blobContainerName', e.target.value)}
                  />
                </div>
                <div className="config-section">
                  <label>
                    <input
                      type="checkbox"
                      checked={config.autoCreateTables}
                      onChange={(e) => handleInputChange('autoCreateTables', e.target.checked)}
                    />
                    Auto-create tables and containers
                  </label>
                </div>
              </div>
            </>
          )}

          {config.provider === 'Local' && (
            <div className="config-section">
              <label>Local Data Path</label>
              <input
                type="text"
                value={config.localDataPath}
                onChange={(e) => handleInputChange('localDataPath', e.target.value)}
                placeholder="./data"
              />
            </div>
          )}

          {config.provider === 'Memory' && (
            <div className="memory-warning">
              ⚠️ In-memory storage will lose all tracking data when the application restarts.
            </div>
          )}

          {testResult && (
            <div className={`test-result ${testResult.healthy ? 'success' : 'error'}`}>
              {testResult.healthy ? '✓' : '✗'} {testResult.message}
            </div>
          )}

          {saveMessage && (
            <div className={`save-message ${saveMessage.includes('Failed') ? 'error' : 'success'}`}>
              {saveMessage}
            </div>
          )}
        </div>

        <div className="storage-config-actions">
          <button 
            onClick={testConnection} 
            disabled={isTesting || (config.provider === 'Azure' && !config.azureConnectionString)}
            className="test-button"
          >
            {isTesting ? 'Testing...' : 'Test Connection'}
          </button>
          
          <button 
            onClick={saveConfiguration} 
            disabled={isLoading}
            className="save-button"
          >
            {isLoading ? 'Saving...' : 'Save Configuration'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default StorageConfig;
