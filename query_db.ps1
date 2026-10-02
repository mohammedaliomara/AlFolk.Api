$connectionString = "Server=db41274.databaseasp.net; Database=db41274; User Id=db41274; Password=6Rd_Pi!8-H3e; Encrypt=False; TrustServerCertificate=True; MultipleActiveResultSets=True;"

$sql = @"
UPDATE Children SET UsageHours = 0;
UPDATE ChildSkills SET Points = 0, Progress = 0;
DELETE FROM GameSessions;
DELETE FROM VideoViews;
DELETE FROM ActivityLogs;
"@

$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$command = New-Object System.Data.SqlClient.SqlCommand($sql, $connection)

try {
    $connection.Open()
    $rowsAffected = $command.ExecuteNonQuery()
    Write-Output "Successfully zeroed out usage and points! Rows affected: $rowsAffected"
}
catch {
    Write-Error "Error executing SQL: $_"
}
finally {
    $connection.Close()
}
