$body = @{
    email = "admin@alfolk.org"
    password = "123456"
} | ConvertTo-Json

try {
    Write-Output "Testing connection to local API on port 5050..."
    $response = Invoke-RestMethod -Uri "http://localhost:5050/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Output "SUCCESS! Successfully logged into local API."
    Write-Output "Token retrieved: $($response.token.Substring(0, 20))..."
    
    Write-Output "Triggering activity simulation..."
    $headers = @{
        Authorization = "Bearer $($response.token)"
    }
    $simResponse = Invoke-RestMethod -Uri "http://localhost:5050/api/admin/simulate-activity" -Method Post -Headers $headers
    Write-Output "SUCCESS! Simulation result: $($simResponse.message)"
}
catch {
    Write-Output "Could not connect to port 5050: $_"
    
    try {
        Write-Output "Testing connection to local API on port 7222..."
        # Bypass SSL verification for localhost HTTPS
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
        $response = Invoke-RestMethod -Uri "https://localhost:7222/api/auth/login" -Method Post -Body $body -ContentType "application/json"
        Write-Output "SUCCESS! Successfully logged into local API on 7222."
        
        Write-Output "Triggering activity simulation..."
        $headers = @{
            Authorization = "Bearer $($response.token)"
        }
        $simResponse = Invoke-RestMethod -Uri "https://localhost:7222/api/admin/simulate-activity" -Method Post -Headers $headers
        Write-Output "SUCCESS! Simulation result: $($simResponse.message)"
    }
    catch {
        Write-Output "Could not connect to port 7222: $_"
    }
}
