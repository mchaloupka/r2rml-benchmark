module Database

open System

#load "shell.fsx"
#load "docker.fsx"

open Docker
open Shell

type Databases =
  | MsSql
  | MySql

let dbName = function
  | MsSql -> "mssql"
  | MySql -> "mysql"

let databaseDockerName = "database"

let startDatabaseContainer = function
  | MsSql ->
    inDocker databaseDockerName "mcr.microsoft.com/mssql/server:2017-latest"
    |> withMount msSqlDatasetDir "/benchmark/dataset"
    |> withEnv "ACCEPT_EULA" "Y"
    |> withEnv "SA_PASSWORD" "p@ssw0rd"
    |> withPort 1433 1433
    |> startContainerDetached

    Threading.Thread.Sleep(15000) // Enough time to start MSSQL server

    IO.DirectoryInfo(msSqlDatasetDir).GetFiles()
      |> Seq.filter (fun x -> x.Extension = ".sql")
      |> Seq.iter (fun x ->
        execInContainer
          databaseDockerName
          (sprintf "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P p@ssw0rd -i /benchmark/dataset/%s\"" x.Name)
      )
      
  | MySql ->
    inDocker databaseDockerName "mysql:latest"
    |> withMount mySqlDatasetDir "/benchmark/dataset"
    |> withEnv "MYSQL_ROOT_PASSWORD" "psw"
    |> withPort 3306 3306
    |> startContainerDetached

    Threading.Thread.Sleep(15000) // Enough time to start MySQL server

    IO.DirectoryInfo(mySqlDatasetDir).GetFiles()
      |> Seq.filter (fun x -> x.Extension = ".sql")
      |> Seq.iter (fun x ->
        execInContainer
          databaseDockerName
          (sprintf "mysql -u root -ppsw -e \"source /benchmark/dataset/%s\"" x.Name)
      )