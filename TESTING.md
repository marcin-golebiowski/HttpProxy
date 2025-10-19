# HTTP Proxy - Testing Guide

This guide explains how to test the HTTP/HTTPS proxy server.

## Running the Proxy Server

First, start the proxy server:

```powershell
cd HttpProxy
dotnet run
```

The proxy will start listening on port **8888** by default.

To use a different port:
```powershell
dotnet run 9000
```

---

## Testing Methods

### Method 1: Automated Test Program (Recommended)

The test program runs comprehensive tests against the proxy.

**Run the test program:**

```powershell
# In a new terminal window (keep proxy running)
cd HttpProxy.Tests
dotnet run
```

**Tests included:**
- ? HTTP GET request
- ? HTTPS GET request  
- ? HTTP POST request with form data
- ? Multiple concurrent requests
- ? Error handling for invalid hosts

---

### Method 2: PowerShell Test Script

Run the manual test script:

```powershell
# Make sure proxy is running first
.\test-proxy.ps1
```

This uses both `curl` and PowerShell's `Invoke-WebRequest` to test the proxy.

---

### Method 3: Manual Testing with curl

```powershell
# HTTP GET
curl -x http://localhost:8888 http://httpbin.org/get

# HTTPS GET
curl -x http://localhost:8888 https://httpbin.org/get -k

# HTTP POST
curl -x http://localhost:8888 -X POST http://httpbin.org/post -d "key=value"

# View response headers
curl -x http://localhost:8888 -I http://httpbin.org/headers
```

---

### Method 4: Browser Testing

Configure your browser to use the proxy:

**Chrome/Edge:**
1. Settings ? System ? Open proxy settings
2. Set HTTP proxy: `localhost`, Port: `8888`
3. Browse any website - traffic will go through the proxy

**Firefox:**
1. Settings ? Network Settings ? Manual proxy configuration
2. HTTP Proxy: `localhost`, Port: `8888`
3. ? Also use this proxy for HTTPS

---

### Method 5: PowerShell Invoke-WebRequest

```powershell
# HTTP request
Invoke-WebRequest -Uri "http://httpbin.org/get" -Proxy "http://localhost:8888"

# HTTPS request
Invoke-WebRequest -Uri "https://httpbin.org/get" -Proxy "http://localhost:8888"

# POST request
Invoke-WebRequest -Uri "http://httpbin.org/post" -Method POST -Body @{key="value"} -Proxy "http://localhost:8888"
```

---

## What to Look For

### In the Proxy Console:
You should see log output for each request:
```
HTTP/HTTPS Proxy Server started on port 8888
Press Ctrl+C to stop the server...
15:30:45 - GET http://httpbin.org/get
15:30:47 - CONNECT httpbin.org:443
```

### Successful Test Results:
- HTTP requests return 200 status codes
- HTTPS requests establish CONNECT tunnels
- POST requests forward data correctly
- Multiple concurrent requests handled properly
- Invalid hosts return appropriate errors (502 Bad Gateway)

---

## Troubleshooting

**"Connection refused" errors:**
- Make sure the proxy server is running
- Check the port number matches (default: 8888)

**HTTPS requests fail:**
- This is normal for strict certificate validation
- Use `-k` flag with curl or disable cert validation in test code

**Tests timeout:**
- Check your internet connection
- Firewall might be blocking the proxy
- Try increasing timeout values

---

## Expected Output Example

### Proxy Server Output:
```
HTTP/HTTPS Proxy Server started on port 8888
Press Ctrl+C to stop the server...
15:30:12 - GET http://httpbin.org/get
15:30:14 - CONNECT httpbin.org:443
15:30:16 - POST http://httpbin.org/post
```

### Test Program Output:
```
=== HTTP Proxy Test Client ===
Testing proxy at: http://localhost:8888

--- Test 1: HTTP GET Request ---
Status: OK
Content Length: 312 characters
Success: True
? HTTP GET test PASSED

--- Test 2: HTTPS GET Request ---
Status: OK
Content Length: 298 characters
Success: True
? HTTPS GET test PASSED

[... more tests ...]

=== All Tests Completed ===
```
