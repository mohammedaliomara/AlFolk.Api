$base = "https://admindash.runasp.net"

$body = @{
    Email    = "admin@alfolk.org"
    Password = "123456"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $loginRes.token
$headers = @{ Authorization = "Bearer $token" }

# Check activity logs
Write-Output "=== GET /api/activitylog ==="
try {
    $logs = Invoke-RestMethod -Uri "$base/api/activitylog?page=1&pageSize=10" -Headers $headers
    $logs | ConvertTo-Json -Depth 4
} catch { Write-Output "Error: $_" }

# Check weekly activity
Write-Output "`n=== GET /api/dashboard/weekly-activity ==="
try {
    $weekly = Invoke-RestMethod -Uri "$base/api/dashboard/weekly-activity" -Headers $headers
    $weekly | ConvertTo-Json -Depth 4
} catch { Write-Output "Error: $_" }

# Check simulate endpoint
Write-Output "`n=== POST /api/admin/simulate-activity ==="
try {
    $sim = Invoke-RestMethod -Uri "$base/api/admin/simulate-activity" -Method Post -Headers $headers
    $sim | ConvertTo-Json -Depth 4
} catch { Write-Output "Error: $_" }
