#!/usr/bin/env node

/**
 * MCP Server Bridge for Claude Desktop
 * This bridges Claude Desktop's stdio MCP protocol to your HTTP-based MCP server
 */

const http = require('http');
const readline = require('readline');

class MCPBridge {
    constructor() {
        this.serverUrl = 'http://localhost:5236';
        //this.serverUrl = 'https://edgarconnector-hqfkbgatdaf6a0cz.northcentralus-01.azurewebsites.net';
        this.setupIO();
    }

    setupIO() {
        // Create readline interface for stdin/stdout
        this.rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout,
            terminal: false
        });

        // Handle incoming messages from Claude Desktop
        this.rl.on('line', (line) => {
            try {
                if (line.trim()) {
                    const message = JSON.parse(line);
                    console.error(`Received from Claude: ${JSON.stringify(message)}`);
                    
                    // Ensure we have a valid ID - Claude Desktop requires this
                    // Note: Don't use !message.id because 0 is a valid ID
                    if (message.id === undefined || message.id === null) {
                        message.id = Date.now().toString();
                    }
                    this.forwardToServer(message);
                }
            } catch (error) {
                console.error(`JSON parse error: ${error.message}`);
                this.sendError('parse-error', 'Invalid JSON received', null);
            }
        });

        // Handle process shutdown
        process.on('SIGINT', () => {
            this.rl.close();
            process.exit(0);
        });
    }

    async forwardToServer(message) {
        const requestData = JSON.stringify(message);
        
        const options = {
            hostname: 'localhost',
            port: 5236,
            path: '/mcp',
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(requestData)
            }
        };

        const req = http.request(options, (res) => {
            let data = '';
            
            res.on('data', (chunk) => {
                data += chunk;
            });
            
            res.on('end', () => {
                try {
                    // Parse the JSON response
                    const response = JSON.parse(data);
                    
                    // Fix protocol version if needed
                    if (response.result && response.result.protocolVersion === "2024-11-05") {
                        response.result.protocolVersion = "2025-06-18";
                    }
                    
                    // Forward the response back to Claude Desktop
                    console.log(JSON.stringify(response));
                } catch (error) {
                    console.error(`Error parsing server response: ${error.message}`);
                    this.sendError('server-error', 'Invalid response from server', message.id);
                }
            });
        });

        req.on('error', (error) => {
            console.error(`Connection error: ${error.message}`);
            this.sendError('connection-error', `Failed to connect to MCP server: ${error.message}`, message.id);
        });

        // Set a timeout for the request
        req.setTimeout(30000, () => {
            req.destroy();
            this.sendError('timeout-error', 'Request to MCP server timed out', message.id);
        });

        req.write(requestData);
        req.end();
    }

    sendError(code, message, id) {
        const errorResponse = {
            jsonrpc: "2.0",
            id: id,
            error: {
                code: -32603,
                message: message,
                data: { type: code }
            }
        };
        console.log(JSON.stringify(errorResponse));
    }

    sendResponse(id, result) {
        const response = {
            jsonrpc: "2.0",
            id: id,
            result: result
        };
        console.log(JSON.stringify(response));
    }
}

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason, promise) => {
    console.error('Unhandled Rejection at:', promise, 'reason:', reason);
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error('Uncaught Exception:', error);
    process.exit(1);
});

// Start the bridge
const bridge = new MCPBridge();

// Send initial capabilities when started
const initResponse = {
    jsonrpc: "2.0",
    id: "init",
    result: {
        protocolVersion: "2024-11-05",
        capabilities: {
            tools: {}
        },
        serverInfo: {
            name: "edgar-processor-bridge",
            version: "1.0.0"
        }
    }
};

// Don't send init response automatically - wait for initialize call
// console.log(JSON.stringify(initResponse));

console.error('MCP Bridge started, forwarding to http://localhost:5236/mcp');
