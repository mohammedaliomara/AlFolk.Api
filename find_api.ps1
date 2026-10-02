$urls = @(
    "https://alfolk.runasp.net",
    "https://kidappadmindash.runasp.net",
    "https://admindash.runasp.net"
)

$body = @{
    Email    = "admin@alfolk.org"
    Password = "123456"
} | ConvertTo-Json

foreach ($base in $urls) {
    Write-Output "=== Testing: $base ==="
    try {
        $res = Invoke-WebRequest -Uri "$base/api/auth/login" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 10 -ErrorAction Stop
        Write-Output "SUCCESS $($res.StatusCode): $($res.Content.Substring(0, [Math]::Min(300, $res.Content.Length)))"
    } catch {
        $msg = $_.Exception.Message
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $bodyText = $reader.ReadToEnd()
            Write-Output "FAIL ($msg) => $bodyText"
        } catch {
            Write-Output "FAIL: $msg"
        }
    }
}
