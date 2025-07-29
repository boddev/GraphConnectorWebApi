import React, { useState, useEffect, useMemo } from 'react';

const CompanySelector = ({ companies, selectedCompanies, onSelectionChange, loading, error }) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [selectAll, setSelectAll] = useState(false);

  // Filter companies based on search term
  const filteredCompanies = useMemo(() => {
    if (!searchTerm.trim()) return companies;
    
    const term = searchTerm.toLowerCase();
    return companies.filter(company => 
      company.ticker.toLowerCase().includes(term) ||
      company.title.toLowerCase().includes(term)
    );
  }, [companies, searchTerm]);

  // Update select all checkbox state based on current selection
  useEffect(() => {
    if (filteredCompanies.length === 0) {
      setSelectAll(false);
    } else {
      const allFilteredSelected = filteredCompanies.every(company => 
        selectedCompanies.some(selected => selected.cik === company.cik && selected.ticker === company.ticker)
      );
      setSelectAll(allFilteredSelected);
    }
  }, [selectedCompanies, filteredCompanies]);

  const handleSearchChange = (e) => {
    setSearchTerm(e.target.value);
  };

  const handleSelectAll = () => {
    if (selectAll) {
      // Deselect all filtered companies
      const filteredKeys = new Set(filteredCompanies.map(c => `${c.cik}-${c.ticker}`));
      const newSelection = selectedCompanies.filter(company => !filteredKeys.has(`${company.cik}-${company.ticker}`));
      onSelectionChange(newSelection);
    } else {
      // Select all filtered companies
      const existingKeys = new Set(selectedCompanies.map(c => `${c.cik}-${c.ticker}`));
      const newCompanies = filteredCompanies.filter(company => !existingKeys.has(`${company.cik}-${company.ticker}`));
      onSelectionChange([...selectedCompanies, ...newCompanies]);
    }
  };

  const handleCompanyToggle = (company) => {
    const isSelected = selectedCompanies.some(selected => selected.cik === company.cik && selected.ticker === company.ticker);
    
    if (isSelected) {
      // Remove from selection
      const newSelection = selectedCompanies.filter(selected => !(selected.cik === company.cik && selected.ticker === company.ticker));
      onSelectionChange(newSelection);
    } else {
      // Add to selection
      onSelectionChange([...selectedCompanies, company]);
    }
  };

  if (loading) {
    return (
      <div className="section">
        <h2>Company Selection</h2>
        <div className="loading">Loading company data from SEC...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="section">
        <h2>Company Selection</h2>
        <div className="error">{error}</div>
      </div>
    );
  }

  return (
    <div className="section">
      <h2>Company Selection</h2>
      
      {/* Statistics */}
      <div className="stats">
        <div className="stat-item">
          <div className="stat-number">{companies.length.toLocaleString()}</div>
          <div className="stat-label">Total Companies</div>
        </div>
        <div className="stat-item">
          <div className="stat-number">{filteredCompanies.length.toLocaleString()}</div>
          <div className="stat-label">Filtered Results</div>
        </div>
        <div className="stat-item">
          <div className="stat-number">{selectedCompanies.length.toLocaleString()}</div>
          <div className="stat-label">Selected</div>
        </div>
      </div>

      {/* Search Input */}
      <div className="search-container">
        <input
          type="text"
          className="search-input"
          placeholder="Search by company name or ticker symbol..."
          value={searchTerm}
          onChange={handleSearchChange}
        />
      </div>

      {/* Selected Companies Display */}
      {selectedCompanies.length > 0 && (
        <div className="selected-companies-section">
          <h3>Selected Companies ({selectedCompanies.length})</h3>
          <div className="selected-companies-list">
            {selectedCompanies.map((company) => (
              <div 
                key={`selected-${company.cik}-${company.ticker}`} 
                className="selected-company-item"
              >
                <div className="selected-company-info">
                  <div className="selected-company-name">{company.title}</div>
                  <div className="selected-company-ticker">
                    {company.ticker} (CIK: {company.cik.toString().padStart(10, '0')})
                  </div>
                </div>
                <button 
                  className="remove-company-btn"
                  onClick={() => handleCompanyToggle(company)}
                  title="Remove from selection"
                >
                  ×
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Select All Checkbox */}
      {filteredCompanies.length > 0 && (
        <div className="select-all-container">
          <label className="select-all-label">
            <input
              type="checkbox"
              checked={selectAll}
              onChange={handleSelectAll}
            />
            {selectAll ? 'Deselect All' : 'Select All'} 
            ({filteredCompanies.length} companies)
          </label>
        </div>
      )}

      {/* Companies List */}
      <div className="companies-list">
        {filteredCompanies.length === 0 ? (
          <div className="no-results">
            {searchTerm ? 'No companies found matching your search.' : 'No companies available.'}
          </div>
        ) : (
          filteredCompanies.map((company) => {
            const isSelected = selectedCompanies.some(selected => selected.cik === company.cik && selected.ticker === company.ticker);
            
            return (
              <div 
                key={`${company.cik}-${company.ticker}`} 
                className="company-item"
                onClick={() => handleCompanyToggle(company)}
              >
                <input
                  type="checkbox"
                  className="company-checkbox"
                  checked={isSelected}
                  onChange={() => {}} // Handled by parent div onClick
                />
                <div className="company-info">
                  <div className="company-name">{company.title}</div>
                  <div className="company-ticker">
                    {company.ticker} (CIK: {company.cik.toString().padStart(10, '0')})
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};

export default CompanySelector;
