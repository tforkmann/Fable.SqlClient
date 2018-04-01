namespace Fable.SqlClient 

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.PowerPack 


module SqlClient = 

    [<Import("*", "mssql")>] 
    let private mssql: IMSSql = jsNative
    [<Emit("$1[$0]")>]
    let private get<'a> (prop: string) (literal: obj) : 'a = jsNative
    let mutable private globalConnectionPool : Option<ISqlConnectionPool> = None 
    let connect (config: SqlConfig list) : Fable.Import.JS.Promise<ISqlConnectionPool> =
        let connectionConfig = SqlConfig.create config 
        match globalConnectionPool with 
        | Some connectionPool -> Promise.lift connectionPool 
        | None -> promise {
            let! pool = mssql.connect connectionConfig
            globalConnectionPool <- Some pool
            return pool
        }

    /// Closes any open connection that is maintained by the global connection pool
    let close() = 
        match globalConnectionPool with 
        | Some pool -> 
            pool.close() 
            globalConnectionPool <- None 
        | None -> 
            () 

    /// Creates a request from the given connection pool.
    let request (pool: ISqlConnectionPool) : ISqlRequest = 
        pool.request() 
    
    /// Executes a qeury on the server that returns a result set with one or more objects of type `'a`
    let query<'a> (query: string) (req: ISqlRequest) : Fable.Import.JS.Promise<Result<'a[], SqlError>> = 
        req.query query
        |> Promise.map (get<'a[]> "recordset" >> Ok)
        |> Promise.catch (unbox<SqlError> >> Error)


    /// Executes a query on the server and returns the first element of result set, if any. 
    let queryScalar<'a> (sqlQuery: string) (req: ISqlRequest) : Fable.Import.JS.Promise<Result<'a, SqlError>> = 
        promise {
            let! results = query sqlQuery req 
            match results with 
            | Ok [| |] ->  
                let sqlError = {
                    name = SqlErrorType.RequestError
                    code = ""
                    message = "Result set was empty"
                }

                return Error sqlError
            | Ok elements  ->
                let element = elements.[0]
                let keys = Fable.Import.JS.Object.keys element
                return Ok (get<'a> keys.[0] element) 
            | Error error -> return Error error
        }

    let private convertSqlType = function 
        | SqlTypes.Char n -> mssql.Char n 
        | SqlTypes.CharMax -> mssql.Char mssql.MAX
        | SqlTypes.NChar n -> mssql.NChar n
        | SqlTypes.NCharMax -> mssql.NChar mssql.MAX
        | SqlTypes.UniqueIdentifier -> mssql.UniqueIdentifier
        | SqlTypes.VarCharMax -> mssql.VarChar mssql.MAX
        | SqlTypes.Int -> mssql.Int 
        | SqlTypes.Float -> mssql.Float
        | SqlTypes.VarChar n -> mssql.VarChar n
        | SqlTypes.BigInt -> mssql.BigInt  
        | SqlTypes.Binary -> mssql.Binary 
        | SqlTypes.VarBinary n -> mssql.VarBinary n
        | SqlTypes.VarBinaryMax -> mssql.VarBinary mssql.MAX
        | SqlTypes.NVarChar n -> mssql.NVarChar n
        | SqlTypes.NVarCharMax -> mssql.NVarChar mssql.MAX
        | SqlTypes.Bit -> mssql.Bit
        | SqlTypes.Date -> mssql.Date 
        | SqlTypes.DateTime -> mssql.DateTime
        | SqlTypes.Money -> mssql.Money 
        | SqlTypes.SmallInt -> mssql.SmallInt
        | SqlTypes.SmallDateTime -> mssql.SmallDateTime
        | SqlTypes.NText -> mssql.NText 
        | SqlTypes.Text -> mssql.Text
        | SqlTypes.Numeric (x, y) -> mssql.Numeric x y
        | SqlTypes.Time n -> mssql.Time n
        | SqlTypes.DateTime2 n -> mssql.DateTime2 n
        | SqlTypes.DateTimeOffset n -> mssql.DateTimeOffset n
        | SqlTypes.Decimal (x, y) -> mssql.Decimal x y
        | SqlTypes.Variant -> mssql.Variant
    let input (name: string) (dataType: SqlTypes) (value: 'a) (req: ISqlRequest) =
        req.input name (convertSqlType dataType) value 
        req
    let output (name: string) (dataType: SqlTypes) (req: ISqlRequest) =
        req.output name (convertSqlType dataType) 
        req

    /// Executes a statement and returns the number of the rows affected
    let executeNonQuery (sqlQuery)  (req: ISqlRequest) : Fable.Import.JS.Promise<Result<int, SqlError>> = 
        promise {
            let! _ = query<obj> sqlQuery req 
            return Ok req.rowsAffected
        } 
        |> Promise.catch (unbox<SqlError> >> Error)

