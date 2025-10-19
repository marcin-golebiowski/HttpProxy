# HTTP Proxy Manual Test Script

Write-Host "=== HTTP Proxy Manual Test Script ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Simple HTTP request using curl
Write-Host "Test 1: HTTP GET via Proxy (using curl)" -ForegroundColor Yellow
Write-Host "Command: curl -x http://localhost:8888 http://httpbin.org/get" -ForegroundColor Gray
try {
    $result = curl.exe -x http://localhost:8888 http://httpbin.org/get 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? SUCCESS" -ForegroundColor Green
        Write-Host "Response preview: $($result[0..5] -join ' ')..." -ForegroundColor Gray
    } else {
        Write-Host "? FAILED" -ForegroundColor Red
    }
} catch {
    Write-Host "? ERROR: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor Gray
Write-Host ""

# Test 2: HTTPS request
Write-Host "Test 2: HTTPS GET via Proxy (using curl)" -ForegroundColor Yellow
Write-Host "Command: curl -x http://localhost:8888 https://httpbin.org/get -k" -ForegroundColor Gray
try {
    $result = curl.exe -x http://localhost:8888 https://httpbin.org/get -k 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? SUCCESS" -ForegroundColor Green
        Write-Host "Response preview: $($result[0..5] -join ' ')..." -ForegroundColor Gray
    } else {
        Write-Host "? FAILED" -ForegroundColor Red
    }
} catch {
    Write-Host "? ERROR: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor Gray
Write-Host ""

# Test 3: POST request
Write-Host "Test 3: HTTP POST via Proxy (using curl)" -ForegroundColor Yellow
Write-Host "Command: curl -x http://localhost:8888 -X POST http://httpbin.org/post -d 'test=data'" -ForegroundColor Gray
try {
    $result = curl.exe -x http://localhost:8888 -X POST http://httpbin.org/post -d "test=data" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? SUCCESS" -ForegroundColor Green
    } else {
        Write-Host "? FAILED" -ForegroundColor Red
    }
} catch {
    Write-Host "? ERROR: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor Gray
Write-Host ""

# Test 4: Using PowerShell's Invoke-WebRequest
Write-Host "Test 4: HTTP GET via Proxy (using PowerShell)" -ForegroundColor Yellow
try {
    $proxy = [System.Net.WebProxy]::new("http://localhost:8888")
    $response = Invoke-WebRequest -Uri "http://httpbin.org/get" -Proxy $proxy.Address -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "? SUCCESS (Status: $($response.StatusCode))" -ForegroundColor Green
        Write-Host "Content Length: $($response.Content.Length) bytes" -ForegroundColor Gray
    } else {
        Write-Host "? FAILED (Status: $($response.StatusCode))" -ForegroundColor Red
    }
} catch {
    Write-Host "? ERROR: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Tests Completed ===" -ForegroundColor Cyan
