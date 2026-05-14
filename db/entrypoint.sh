#!/bin/bash

/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

echo "Waiting for SQL Server to start..."
i=0
while [ $i -lt 30 ]; do
    if /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -No -Q "SELECT 1" > /dev/null 2>&1; then
        echo "SQL Server is ready."
        break
    fi
    i=$((i + 1))
    sleep 3
done

DB_EXISTS=$(/opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -No -h-1 \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='LibraryDB'" 2>/dev/null | tr -d ' \r\n')

if [ "$DB_EXISTS" = "0" ]; then
    echo "Initializing LibraryDB..."
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -No -i /init/01_schema.sql
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -No -d LibraryDB -i /init/02_procedures.sql
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U SA -P "$MSSQL_SA_PASSWORD" -No -d LibraryDB -i /init/03_seed.sql
    echo "LibraryDB ready."
else
    echo "LibraryDB already exists, skipping initialization."
fi

wait $MSSQL_PID
