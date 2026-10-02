$body = @{
    Email = "admin@alfolk.org"
    Password = "123456"
} | ConvertTo-Json

try {
    $res = Invoke-WebRequest -Uri "https://alfolk.runasp.net/api/auth/login" -Method Post -Body $body -ContentType "application/json"
    Write-Output "Status: $($res.StatusCode)"
    Write-Output "Body: $($res.Content)"
}
catch {
    Write-Output "Exception: $_"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $body = $reader.ReadToEnd()
        Write-Output "Response body: $body"
    }
}
