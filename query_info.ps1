$connectionString = "Server=tcp:db41274.databaseasp.net,1433; Database=db41274; User Id=db41274; Password=6Rd_Pi!8-H3e; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;"

$sql = @"
SELECT Id, FirstName, LastName, Role FROM Users;
SELECT Id, Name, ParentId FROM Children;
SELECT Id, Name FROM Games;
"@

$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$command = New-Object System.Data.SqlClient.SqlCommand($sql, $connection)

try {
    $connection.Open()
    $reader = $command.ExecuteReader()
    
    Write-Output "--- USERS ---"
    while ($reader.Read()) {
        Write-Output "$($reader["Id"]) | $($reader["FirstName"]) $($reader["LastName"]) | $($reader["Role"])"
    }
    
    if ($reader.NextResult()) {
        Write-Output "--- CHILDREN ---"
        while ($reader.Read()) {
            Write-Output "$($reader["Id"]) | $($reader["Name"]) | $($reader["ParentId"])"
        }
    }

    if ($reader.NextResult()) {
        Write-Output "--- GAMES ---"
        while ($reader.Read()) {
            Write-Output "$($reader["Id"]) | $($reader["Name"])"
        }
    }
}
catch {
    Write-Error "Error: $_"
}
finally {
    $connection.Close()
}
