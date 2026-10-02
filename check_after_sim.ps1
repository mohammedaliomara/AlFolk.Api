$base = "https://admindash.runasp.net"

$body = @{
    Email    = "admin@alfolk.org"
    Password = "123456"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $loginRes.token
$headers = @{ Authorization = "Bearer $token" }

# Get all parents and show loginCount
Write-Output "=== Parents LoginCount after simulation ==="
$res = Invoke-RestMethod -Uri "$base/api/parents?page=1&pageSize=20&sortBy=LoginCount&sortDir=desc" -Headers $headers
foreach ($p in $res.data) {
    Write-Output "Name: $($p.name) | LoginCount: $($p.loginCount) | Score: $($p.totalChildrenScore) | Videos: $($p.watchedVideosCount)"
}

# Also check weekly activity now
Write-Output "`n=== Weekly Activity ==="
$weekly = Invoke-RestMethod -Uri "$base/api/dashboard/weekly-activity" -Headers $headers
foreach ($d in $weekly.data) {
    Write-Output "$($d.date) | Kids: $($d.activeKids) | Parents: $($d.activeParents)"
}
