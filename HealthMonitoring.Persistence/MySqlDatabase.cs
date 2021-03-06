﻿using System;
using System.Configuration;
using System.Data;
using System.Linq;
using Dapper;
using MySql.Data.MySqlClient;

namespace HealthMonitoring.Persistence
{
    public class MySqlDatabase
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["HealthMonitoring"].ConnectionString;
        private readonly string _databaseName = ConfigurationManager.AppSettings["DatabaseName"];

        public MySqlDatabase()
        {
            CreateDatabaseIfNeeded();
        }

        public IDbConnection OpenConnection()
        {
            var mySqlConnectionStringBuilder = new MySqlConnectionStringBuilder(_connectionString)
            {
                Database = _databaseName
            };
            var mySqlConnection = new MySqlConnection(mySqlConnectionStringBuilder.ToString());
            mySqlConnection.Open();
            return mySqlConnection;
        }

        private void CreateDatabaseIfNeeded()
        {
            using (var conn = new MySqlConnection(_connectionString))
                conn.Execute($"CREATE DATABASE IF NOT EXISTS {_databaseName}");

            using (var conn = OpenConnection())
            {
                if (!DoesTableExists(conn, "EndpointConfig"))
                    CreateEndpointConfig(conn);
                if (!DoesColumnExists(conn, "EndpointConfig", "Tags"))
                    CreateColumn(conn, "EndpointConfig", "Tags", "varchar(4096)");
                if (!DoesColumnExists(conn, "EndpointConfig", "Password"))
                    CreateColumn(conn, "EndpointConfig", "Password", "varchar(64)");
                var date = DateTime.UtcNow;
                if (!DoesColumnExists(conn, "EndpointConfig", "RegisteredOnUtc"))
                    CreateColumnAndSetValue(conn, "EndpointConfig", "RegisteredOnUtc", "datetime not null", date);
                if (!DoesColumnExists(conn, "EndpointConfig", "RegistrationUpdatedOnUtc"))
                    CreateColumnAndSetValue(conn, "EndpointConfig", "RegistrationUpdatedOnUtc", "datetime not null", date);
                if (!DoesColumnExists(conn, "EndpointConfig", "MonitorTag"))
                    CreateColumn(conn, "EndpointConfig", "MonitorTag", "varchar(1024) default 'default'");
                if (!DoesTableExists(conn, "EndpointStats"))
                    CreateEndpointStats(conn);
                if (!DoesTableExists(conn, "HealthMonitorTypes"))
                    CreateHealthMonitorTypesTable(conn);
                if (!DoesIndexExists(conn, "EndpointStats", "EndpointStats_EndpointIdCheckTimeUtc_idx"))
                    CreateEndpointStatsIndex(conn);
                if (!DoesIndexExists(conn, "EndpointConfig", "EndpointConfig_MonitorTag_idx"))
                    CreateMonitorTagIndex(conn);
                if (!DoesIndexExists(conn, "EndpointConfig", "EndpointConfig_MonitorType_idx"))
                    CreateMonitorTypeIndex(conn);
            }
        }

        private bool DoesTableExists(IDbConnection conn, string tableName)
        {
            var query = $@"SELECT 1
FROM information_schema.TABLES
WHERE
TABLE_NAME='{tableName}'
AND TABLE_SCHEMA='{_databaseName}'";

            return conn
                .Query(query)
                .Any();
        }

        private bool DoesIndexExists(IDbConnection conn, string tableName, string indexName)
        {
            var query = $@"show index from {tableName} where Key_name='{indexName}'";

            return conn
                .Query(query)
                .Any();
        }

        private static void CreateEndpointConfig(IDbConnection conn)
        {
            conn.Execute(@"
create table EndpointConfig (
    Id char(36) primary key, 
    MonitorType varchar(100) not null, 
    Address varchar(2048) not null, 
    GroupName varchar(1024) not null, 
    Name varchar(1024) not null,
    Tags varchar(4096),
    Password char(64),
    RegisteredOnUtc datetime not null,
    RegistrationUpdatedOnUtc datetime not null,
    MonitorTag varchar(1024) not null default 'default'
);

create index EndpointConfig_MonitorType_idx on EndpointConfig(MonitorType);
create index EndpointConfig_MonitorTag_idx on EndpointConfig(MonitorTag);
");
        }

        private void CreateMonitorTagIndex(IDbConnection conn)
        {
            conn.Execute("create index EndpointConfig_MonitorTag_idx on EndpointConfig(MonitorTag)");
        }
        private void CreateMonitorTypeIndex(IDbConnection conn)
        {
            conn.Execute("create index EndpointConfig_MonitorType_idx on EndpointConfig(MonitorType)");
        }

        private static void CreateEndpointStatsIndex(IDbConnection conn)
        {
            conn.Execute("create index EndpointStats_EndpointIdCheckTimeUtc_idx on EndpointStats(EndpointId,CheckTimeUtc)");
        }

        private void CreateHealthMonitorTypesTable(IDbConnection conn)
        {
            conn.Execute("create table HealthMonitorTypes(MonitorType varchar(256) primary key)");
        }

        private void CreateEndpointStats(IDbConnection conn)
        {
            conn.Execute(@"
create table EndpointStats (
    Id char(36) primary key,
    EndpointId char(36) not null,
    CheckTimeUtc datetime not null,
    ResponseTime integer not null,
    Status integer not null
);

create index EndpointStats_EndpointId_idx on EndpointStats(EndpointId);
create index EndpointStats_CheckTimeUtc_idx on EndpointStats(CheckTimeUtc);
create index EndpointStats_EndpointIdCheckTimeUtc_idx on EndpointStats(EndpointId,CheckTimeUtc);
");
        }

        private void CreateColumn(IDbConnection conn, string tableName, string columnName, string columnDefinition)
        {
            conn.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
        }

        private void CreateColumnAndSetValue(IDbConnection conn, string tableName, string columnName, string columnDefinition, object value)
        {
            conn.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
            conn.Execute($"UPDATE {tableName} SET {columnName} = @value;", new { value });
        }

        private bool DoesColumnExists(IDbConnection conn, string tableName, string columnName)
        {
            var query = $@"SELECT 1
FROM information_schema.COLUMNS
WHERE
TABLE_SCHEMA = '{_databaseName}'
AND TABLE_NAME = '{tableName}'
AND COLUMN_NAME = '{columnName}'";

            return conn.Query(query).Any();
        }
    }
}