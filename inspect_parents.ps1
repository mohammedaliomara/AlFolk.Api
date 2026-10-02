$base = "https://admindash.runasp.net"

$body = @{
    Email    = "admin@alfolk.org"
    Password = "123456"
} | ConvertTo-Json

# 1. Login
$loginRes = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $loginRes.token
Write-Output "Logged in. Token: $($token.Substring(0,30))..."

$headers = @{ Authorization = "Bearer $token" }

# 2. Get Parents list
Write-Output "`n=== GET /api/parents ==="
$parents = Invoke-RestMethod -Uri "$base/api/parents?page=1&pageSize=5" -Headers $headers
$parents | ConvertTo-Json -Depth 5

# 3. Get dashboard summary
Write-Output "`n=== GET /api/dashboard/summary ==="
$summary = Invoke-RestMethod -Uri "$base/api/dashboard/summary" -Headers $headers
$summary | ConvertTo-Json -Depth 5
