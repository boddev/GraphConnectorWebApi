import React, { useState, useEffect } from 'react';
import { apiService } from '../services/apiService';
import './DataCollectionConfig.css';
import './DataCollectionConfig.css';

const DataCollectionConfig = () => {
  const [config, setConfig] = useState({
    yearsOfData: 3,
    includedFormTypes: ['10-K', '10-Q', '8-K', 'DEF 14A']
  });
  const [originalConfig, setOriginalConfig] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const availableFormTypes = [
    '10-K', '10-Q', '8-K', 'DEF 14A', '10-K/A', '10-Q/A', '8-K/A',
    'S-1', 'S-3', 'S-4', 'S-8', 'S-11', '424B', 'DEFM14A', 'DEFR14A'
  ];

  useEffect(() => {
    fetchConfig();
  }, []);

  const fetchConfig = async () => {
    try {
      setLoading(true);
      const response = await apiService.getDataCollectionConfig();
      setConfig(response);
      setOriginalConfig(response);
      setError('');
    } catch (err) {
      console.error('Error fetching config:', err);
      setError('Failed to load configuration');
    } finally {
      setLoading(false);
    }
  };

  const handleYearsChange = (e) => {
    const years = parseInt(e.target.value);
    if (years >= 1 && years <= 10) {
      setConfig(prev => ({ ...prev, yearsOfData: years }));
      setSuccess('');
    }
  };

  const handleFormTypeToggle = (formType) => {
    setConfig(prev => {
      const newFormTypes = prev.includedFormTypes.includes(formType)
        ? prev.includedFormTypes.filter(type => type !== formType)
        : [...prev.includedFormTypes, formType];
      
      return { ...prev, includedFormTypes: newFormTypes };
    });
    setSuccess('');
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      setError('');
      setSuccess('');
      
      await apiService.saveDataCollectionConfig(config);
      setOriginalConfig(config);
      setSuccess('Configuration saved successfully! Changes will apply to new crawls.');
    } catch (err) {
      console.error('Error saving config:', err);
      setError('Failed to save configuration');
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    if (originalConfig) {
      setConfig(originalConfig);
      setSuccess('');
      setError('');
    }
  };

  const hasChanges = originalConfig && (
    config.yearsOfData !== originalConfig.yearsOfData ||
    JSON.stringify(config.includedFormTypes.sort()) !== JSON.stringify(originalConfig.includedFormTypes.sort())
  );

  if (loading) {
    return (
      <div className="config-card">
        <div className="loading">Loading configuration...</div>
      </div>
    );
  }

  return (
    <div className="config-card">
      <h3 className="config-title">Data Collection Configuration</h3>
      
      {error && (
        <div className="alert alert-error">
          {error}
        </div>
      )}
      
      {success && (
        <div className="alert alert-success">
          {success}
        </div>
      )}

      <div className="config-section">
        <h4 className="section-subtitle">Years of Historical Data</h4>
        <div className="years-config">
          <label className="config-label">
            Number of years to collect (1-10):
          </label>
          <div className="years-input-container">
            <input
              type="number"
              min="1"
              max="10"
              value={config.yearsOfData}
              onChange={handleYearsChange}
              className="years-input"
            />
            <span className="years-help">
              Currently collecting {config.yearsOfData} year{config.yearsOfData !== 1 ? 's' : ''} of data
            </span>
          </div>
        </div>
      </div>

      <div className="config-section">
        <h4 className="section-subtitle">SEC Form Types to Collect</h4>
        <p className="section-description">
          Select which SEC form types you want to include in your crawls:
        </p>
        
        <div className="form-types-grid">
          {availableFormTypes.map(formType => (
            <label key={formType} className="form-type-checkbox">
              <input
                type="checkbox"
                checked={config.includedFormTypes.includes(formType)}
                onChange={() => handleFormTypeToggle(formType)}
              />
              <span className="checkbox-label">{formType}</span>
            </label>
          ))}
        </div>
        
        <div className="selected-summary">
          <strong>Selected:</strong> {config.includedFormTypes.length} form type{config.includedFormTypes.length !== 1 ? 's' : ''}
          {config.includedFormTypes.length > 0 && (
            <span className="selected-forms">
              ({config.includedFormTypes.join(', ')})
            </span>
          )}
        </div>
      </div>

      <div className="config-actions">
        <button
          onClick={handleSave}
          disabled={saving || !hasChanges}
          className={`save-button ${!hasChanges ? 'disabled' : ''}`}
        >
          {saving ? 'Saving...' : 'Save Configuration'}
        </button>
        
        <button
          onClick={handleReset}
          disabled={saving || !hasChanges}
          className={`reset-button ${!hasChanges ? 'disabled' : ''}`}
        >
          Reset Changes
        </button>
        
        <button
          onClick={fetchConfig}
          disabled={saving}
          className="refresh-button"
        >
          Refresh
        </button>
      </div>

      <div className="config-info">
        <p className="info-text">
          <strong>Note:</strong> Configuration changes will apply to new crawls. 
          Existing tracked documents are not affected.
        </p>
        <p className="info-text">
          Last updated: {originalConfig?.lastUpdated ? 
            new Date(originalConfig.lastUpdated).toLocaleString() : 'Never'}
        </p>
      </div>
    </div>
  );
};

export default DataCollectionConfig;
