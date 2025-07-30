import React, { useState, useEffect } from 'react';
import { apiService, getSchedulerConfig, saveSchedulerConfig } from '../services/apiService';
import './DataCollectionConfig.css';

const DataCollectionConfig = () => {
  const [config, setConfig] = useState({
    yearsOfData: 3,
    includedFormTypes: ['10-K', '10-Q', '8-K', 'DEF 14A']
  });
  const [originalConfig, setOriginalConfig] = useState(null);
  const [schedulerConfig, setSchedulerConfig] = useState({
    enabled: false,
    frequency: 'Weekly',
    hour: 9,
    dayOfWeek: 1,
    dayOfMonth: 1,
    lastScheduledRun: null,
    nextScheduledRun: null
  });
  const [originalSchedulerConfig, setOriginalSchedulerConfig] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [schedulerSaving, setSchedulerSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [schedulerError, setSchedulerError] = useState('');
  const [schedulerSuccess, setSchedulerSuccess] = useState('');

  const availableFormTypes = [
    '10-K', '10-Q', '8-K', 'DEF 14A', '10-K/A', '10-Q/A', '8-K/A',
    'S-1', 'S-3', 'S-4', 'S-8', 'S-11', '424B', 'DEFM14A', 'DEFR14A'
  ];

  useEffect(() => {
    fetchConfig();
    fetchSchedulerConfig();
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

  const fetchSchedulerConfig = async () => {
    try {
      const response = await getSchedulerConfig();
      setSchedulerConfig(response);
      setOriginalSchedulerConfig(response);
      setSchedulerError('');
    } catch (err) {
      console.error('Error fetching scheduler config:', err);
      setSchedulerError('Failed to load scheduler configuration');
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

  const handleSchedulerSave = async () => {
    try {
      setSchedulerSaving(true);
      setSchedulerError('');
      setSchedulerSuccess('');
      
      await saveSchedulerConfig(schedulerConfig);
      setOriginalSchedulerConfig(schedulerConfig);
      setSchedulerSuccess('Scheduler configuration saved successfully!');
    } catch (err) {
      console.error('Error saving scheduler config:', err);
      setSchedulerError('Failed to save scheduler configuration');
    } finally {
      setSchedulerSaving(false);
    }
  };

  const handleSchedulerReset = () => {
    if (originalSchedulerConfig) {
      setSchedulerConfig(originalSchedulerConfig);
      setSchedulerSuccess('');
      setSchedulerError('');
    }
  };

  const handleSchedulerChange = (field, value) => {
    setSchedulerConfig(prev => ({ ...prev, [field]: value }));
    setSchedulerSuccess('');
  };

  const formatDateTime = (dateString) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleString();
  };

  const getNextRunDescription = () => {
    if (!schedulerConfig.enabled) return 'Disabled';
    if (!schedulerConfig.nextScheduledRun) return 'Will be calculated when enabled';
    
    const nextRun = new Date(schedulerConfig.nextScheduledRun);
    const now = new Date();
    const diffMs = nextRun - now;
    const diffHours = Math.round(diffMs / (1000 * 60 * 60));
    
    if (diffHours < 24) {
      return `In ${diffHours} hours (${formatDateTime(schedulerConfig.nextScheduledRun)})`;
    } else {
      const diffDays = Math.round(diffHours / 24);
      return `In ${diffDays} days (${formatDateTime(schedulerConfig.nextScheduledRun)})`;
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

      {/* Scheduler Configuration Section */}
      <div className="config-section">
        <h4 className="section-subtitle">🕐 Automatic Recrawl Scheduler</h4>
        <p className="section-description">
          Configure automatic recrawling of previously crawled companies to keep your data up to date:
        </p>
        
        {schedulerError && (
          <div className="error">
            {schedulerError}
          </div>
        )}

        {schedulerSuccess && (
          <div className="success">
            {schedulerSuccess}
          </div>
        )}

        <div className="scheduler-config">
          <div className="config-option">
            <label className="option-label">
              <input
                type="checkbox"
                checked={schedulerConfig.enabled}
                onChange={(e) => handleSchedulerChange('enabled', e.target.checked)}
              />
              <span className="checkbox-label">Enable automatic recrawling</span>
            </label>
          </div>

          {schedulerConfig.enabled && (
            <>
              <div className="config-option">
                <label className="option-label">
                  Frequency:
                  <select
                    value={schedulerConfig.frequency}
                    onChange={(e) => handleSchedulerChange('frequency', e.target.value)}
                    className="frequency-select"
                  >
                    <option value="Daily">Daily</option>
                    <option value="Weekly">Weekly</option>
                    <option value="Monthly">Monthly</option>
                  </select>
                </label>
              </div>

              <div className="config-option">
                <label className="option-label">
                  Time of day:
                  <select
                    value={schedulerConfig.hour}
                    onChange={(e) => handleSchedulerChange('hour', parseInt(e.target.value))}
                    className="hour-select"
                  >
                    {Array.from({length: 24}, (_, i) => (
                      <option key={i} value={i}>
                        {i === 0 ? '12:00 AM' : 
                         i < 12 ? `${i}:00 AM` : 
                         i === 12 ? '12:00 PM' : `${i - 12}:00 PM`}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              {schedulerConfig.frequency === 'Weekly' && (
                <div className="config-option">
                  <label className="option-label">
                    Day of week:
                    <select
                      value={schedulerConfig.dayOfWeek}
                      onChange={(e) => handleSchedulerChange('dayOfWeek', parseInt(e.target.value))}
                      className="day-select"
                    >
                      <option value={0}>Sunday</option>
                      <option value={1}>Monday</option>
                      <option value={2}>Tuesday</option>
                      <option value={3}>Wednesday</option>
                      <option value={4}>Thursday</option>
                      <option value={5}>Friday</option>
                      <option value={6}>Saturday</option>
                    </select>
                  </label>
                </div>
              )}

              {schedulerConfig.frequency === 'Monthly' && (
                <div className="config-option">
                  <label className="option-label">
                    Day of month:
                    <select
                      value={schedulerConfig.dayOfMonth}
                      onChange={(e) => handleSchedulerChange('dayOfMonth', parseInt(e.target.value))}
                      className="day-select"
                    >
                      {Array.from({length: 31}, (_, i) => (
                        <option key={i + 1} value={i + 1}>{i + 1}</option>
                      ))}
                    </select>
                  </label>
                </div>
              )}

              <div className="scheduler-status">
                <div className="status-item">
                  <strong>Last run:</strong> {formatDateTime(schedulerConfig.lastScheduledRun)}
                </div>
                <div className="status-item">
                  <strong>Next run:</strong> {getNextRunDescription()}
                </div>
              </div>
            </>
          )}
        </div>

        <div className="scheduler-actions">
          <button
            onClick={handleSchedulerSave}
            disabled={schedulerSaving}
            className="save-button"
          >
            {schedulerSaving ? 'Saving...' : 'Save Scheduler Settings'}
          </button>
          
          <button
            onClick={handleSchedulerReset}
            disabled={schedulerSaving}
            className="reset-button"
          >
            Reset Changes
          </button>
          
          <button
            onClick={fetchSchedulerConfig}
            disabled={schedulerSaving}
            className="refresh-button"
          >
            Refresh Status
          </button>
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
