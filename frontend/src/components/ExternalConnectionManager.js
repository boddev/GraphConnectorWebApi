import React, { useState, useEffect, useCallback } from 'react';
import './ExternalConnectionManager.css';

const ExternalConnectionManager = ({ onConnectionSelect }) => {
  const [connections, setConnections] = useState([]);
  const [selectedConnectionId, setSelectedConnectionId] = useState('');
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [newConnection, setNewConnection] = useState({
    id: '',
    name: '',
    description: ''
  });

  const fetchConnections = useCallback(async () => {
    try {
      setLoading(true);
      const response = await fetch('/external-connections');

      if (!response.ok) {
        throw new Error(`Failed to fetch connections: ${response.statusText}`);
      }

      const data = await response.json();
      setConnections(data);

      // Select a connection only if none selected already.
      // Prefer an explicit default if present; otherwise pick the first available.
      const preferred = data.find(conn => conn.isDefault) || data[0];
      if (preferred) {
        setSelectedConnectionId(prev => {
          if (!prev) {
            if (onConnectionSelect) {
              onConnectionSelect(preferred.id);
            }
            return preferred.id;
          }
          return prev;
        });
      }
    } catch (err) {
      console.error('Error fetching connections:', err);
      setError('Failed to load connections');
    } finally {
      setLoading(false);
    }
  }, [onConnectionSelect]);

  useEffect(() => {
    fetchConnections();
  }, [fetchConnections]);

  const handleConnectionSelect = (connectionId) => {
    setSelectedConnectionId(connectionId);
    if (onConnectionSelect) {
      onConnectionSelect(connectionId);
    }
  };

  const handleCreateConnection = async (e) => {
    e.preventDefault();
    
    if (!newConnection.id || !newConnection.name) {
      setError('Connection ID and Name are required');
      return;
    }

    try {
      setLoading(true);
      setError('');
      setSuccess('');

      const response = await fetch('/external-connections', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(newConnection),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || 'Failed to create connection');
      }

      const createdConnection = await response.json();
      setSuccess(`Successfully created connection: ${createdConnection.name}`);
      
      // Reset form
      setNewConnection({ id: '', name: '', description: '' });
      setShowCreateForm(false);
      
      // Refresh connections list
      await fetchConnections();
      
      // Auto-select the new connection
      setSelectedConnectionId(createdConnection.id);
      if (onConnectionSelect) {
        onConnectionSelect(createdConnection.id);
      }

    } catch (err) {
      console.error('Error creating connection:', err);
      setError(err.message || 'Failed to create connection');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteConnection = async (connectionId) => {
    if (!window.confirm('Are you sure you want to delete this connection? This action cannot be undone.')) {
      return;
    }

    try {
      setLoading(true);
      setError('');
      setSuccess('');

      const response = await fetch(`/external-connections/${connectionId}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || 'Failed to delete connection');
      }

      setSuccess('Connection deleted successfully');
      
      // Refresh connections list
      await fetchConnections();
      
      // If deleted connection was selected, select the first available one
      if (selectedConnectionId === connectionId) {
        const remainingConnections = connections.filter(conn => conn.id !== connectionId);
        if (remainingConnections.length > 0) {
          handleConnectionSelect(remainingConnections[0].id);
        } else {
          setSelectedConnectionId('');
          if (onConnectionSelect) {
            onConnectionSelect('');
          }
        }
      }

    } catch (err) {
      console.error('Error deleting connection:', err);
      setError(err.message || 'Failed to delete connection');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="external-connection-manager">
      <div className="connection-manager-header">
        <h3>External Connection Manager</h3>
        <button
          className="btn btn-primary"
          onClick={() => setShowCreateForm(!showCreateForm)}
          disabled={loading}
        >
          {showCreateForm ? 'Cancel' : 'Create New Connection'}
        </button>
      </div>

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

      {showCreateForm && (
        <div className="create-connection-form">
          <h4>Create New External Connection</h4>
          <form onSubmit={handleCreateConnection}>
            <div className="form-group">
              <label htmlFor="connectionId">Connection ID *</label>
              <input
                id="connectionId"
                type="text"
                value={newConnection.id}
                onChange={(e) => setNewConnection(prev => ({ ...prev, id: e.target.value }))}
                placeholder="e.g., mycompany-documents"
                required
                disabled={loading}
              />
              <small className="form-text">Must be unique and contain only lowercase letters, numbers, and hyphens</small>
            </div>

            <div className="form-group">
              <label htmlFor="connectionName">Connection Name *</label>
              <input
                id="connectionName"
                type="text"
                value={newConnection.name}
                onChange={(e) => setNewConnection(prev => ({ ...prev, name: e.target.value }))}
                placeholder="e.g., My Company Documents"
                required
                disabled={loading}
              />
            </div>

            <div className="form-group">
              <label htmlFor="connectionDescription">Description</label>
              <textarea
                id="connectionDescription"
                value={newConnection.description}
                onChange={(e) => setNewConnection(prev => ({ ...prev, description: e.target.value }))}
                placeholder="Brief description of this connection's purpose"
                rows="3"
                disabled={loading}
              />
            </div>

            <div className="form-actions">
              <button type="submit" className="btn btn-primary" disabled={loading}>
                {loading ? 'Creating...' : 'Create Connection'}
              </button>
              <button 
                type="button" 
                className="btn btn-secondary" 
                onClick={() => setShowCreateForm(false)}
                disabled={loading}
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="connection-selector">
        <h4>Active Connection</h4>
        {loading ? (
          <div className="loading">Loading connections...</div>
        ) : (
          <>
            <div className="connection-list">
              {connections.map((connection) => (
                <div
                  key={connection.id}
                  className={`connection-item ${selectedConnectionId === connection.id ? 'selected' : ''}`}
                >
                  <div className="connection-info" onClick={() => handleConnectionSelect(connection.id)}>
                    <div className="connection-header">
                      <strong>{connection.name}</strong>
                      {connection.isDefault && <span className="default-badge">Default</span>}
                    </div>
                    <div className="connection-id">{connection.id}</div>
                    <div className="connection-description">{connection.description}</div>
                    <div className="connection-date">
                      Created: {new Date(connection.createdDate).toLocaleDateString()}
                    </div>
                  </div>
                  {!connection.isDefault && (
                    <button
                      className="btn btn-danger btn-small"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDeleteConnection(connection.id);
                      }}
                      disabled={loading}
                      title="Delete Connection"
                    >
                      Delete
                    </button>
                  )}
                </div>
              ))}
            </div>
            
            {connections.length === 0 && (
              <div className="no-connections">
                No connections found. Create one to get started.
              </div>
            )}
          </>
        )}
      </div>

      {selectedConnectionId && (
        <div className="selected-connection-info">
          <strong>Data will be loaded into connection: </strong>
          <code>{selectedConnectionId}</code>
        </div>
      )}
    </div>
  );
};

export default ExternalConnectionManager;
