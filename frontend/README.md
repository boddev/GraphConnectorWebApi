# Edgar SEC Filings Connector Frontend

A React-based frontend for the Edgar SEC Filings Connector that provides an intuitive interface for selecting companies and triggering SEC filing crawls.

## Features

- **Company Search**: Download and search through all SEC-registered companies
- **Real-time Filtering**: Filter companies by ticker symbol or company name as you type
- **Bulk Selection**: Select individual companies or use "Select All" functionality
- **Crawl Management**: Trigger background crawling processes for selected companies
- **In-Memory Storage**: No Azure dependencies - uses in-memory data storage

## Prerequisites

- Node.js 16+ and npm
- .NET 8 backend API running on https://localhost:7034

## Installation

1. Navigate to the frontend directory:
   ```bash
   cd frontend
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the development server:
   ```bash
   npm start
   ```

4. Open your browser to http://localhost:3000

## Usage

1. **Load Company Data**: The application automatically downloads the latest company ticker data from the SEC when it loads.

2. **Search Companies**: Use the search box to filter companies by ticker symbol or company name. The list updates in real-time as you type.

3. **Select Companies**: 
   - Click on individual companies to select/deselect them
   - Use the "Select All" checkbox to select all filtered companies
   - View the count of selected companies

4. **Trigger Crawl**: Click the "Start Crawl" button to begin processing SEC filings for the selected companies. The crawl runs in the background.

## API Endpoints

The frontend communicates with the following backend endpoints:

- `POST /loadcontent` - Trigger crawl for selected companies
- `POST /provisionconnection` - Provision Graph connection
- `POST /grantPermissions` - Grant necessary permissions

## Architecture

- **React 18** with functional components and hooks
- **Axios** for HTTP requests
- **CSS3** with responsive design
- **SEC API Integration** for real-time company data
- **CORS-enabled** backend communication

## Development

### File Structure
```
frontend/
├── public/
│   └── index.html
├── src/
│   ├── components/
│   │   ├── CompanySelector.js
│   │   └── CrawlControls.js
│   ├── services/
│   │   └── apiService.js
│   ├── App.js
│   ├── index.js
│   └── index.css
└── package.json
```

### Key Components

- **CompanySelector**: Handles company search, filtering, and selection
- **CrawlControls**: Manages crawl operations and displays status
- **apiService**: Handles all API communications

## Building for Production

```bash
npm run build
```

This creates an optimized production build in the `build` folder.

## Notes

- The application eliminates Azure Table Storage dependencies by using in-memory data
- Company data is fetched directly from the SEC's public API
- The interface is designed for ease of use with large datasets (10,000+ companies)
- All crawl operations run asynchronously in the background
