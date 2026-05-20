#!/bin/bash
echo "Waiting for SQL Server to start..."
sleep 20

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_password123" -C \
  -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '_API_shop') CREATE DATABASE [_API_shop]"

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_password123" -C -d _API_shop -i /init/init.sql

echo "Database initialized!"
