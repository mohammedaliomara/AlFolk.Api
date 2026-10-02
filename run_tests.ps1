$baseUrl = "http://localhost:5050/api"

function Test-Endpoint {
    param($method, $path, $body, $token)
    
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers.Add("Authorization", "Bearer $token") }
    
    try {
        $params = @{
            Method = $method
            Uri = "$baseUrl$path"
            Headers = $headers
        }
        if ($body) { $params.Add("Body", ($body | ConvertTo-Json)) }
        
        $response = Invoke-RestMethod @params
        Write-Host "[OK] $method $path" -ForegroundColor Green
        return $response
    } catch {
        Write-Host "[FAIL] $method $path - $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

Write-Host "Starting API Tests..." -Cyan

# 1. Login
$loginBody = @{ email = "admin@alfolk.org"; password = "123456" }
$loginRes = Test-Endpoint "POST" "/auth/login" $loginBody
if ($null -eq $loginRes) { exit }
$token = $loginRes.token

# 2. Test some GET endpoints
Test-Endpoint "GET" "/auth/me" $null $token
Test-Endpoint "GET" "/parents" $null $token
Test-Endpoint "GET" "/children" $null $token
Test-Endpoint "GET" "/analytics/dashboard" $null $token
Test-Endpoint "GET" "/search?q=طفل" $null $token

Write-Host "Testing completed." -Cyan
