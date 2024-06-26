﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using Vit.Linq.ExpressionTree.ComponentModel;
using Vit.Linq;
using Vitorm.Sql.Transaction;
using Vitorm.Sql.SqlTranslate;
using Vitorm.StreamQuery;
using Vit.Extensions.Vitorm_Extensions;

namespace Vitorm.Sql
{
    public partial class SqlDbContext : DbContext
    {
        protected Func<IDbConnection> createDbConnection { get; set; }
        protected IDbConnection _dbConnection;
        public override void Dispose()
        {
            base.Dispose();

            transactionScope?.Dispose();
            transactionScope = null;

            _dbConnection?.Dispose();
            _dbConnection = null;
        }
        public virtual IDbConnection dbConnection => _dbConnection ??= createDbConnection();


        public virtual ISqlTranslateService sqlTranslateService { get; private set; }

        public virtual void Init(ISqlTranslateService sqlTranslateService, Func<IDbConnection> createDbConnection, SqlExecutor sqlExecutor=null)
        {
            this.sqlTranslateService = sqlTranslateService;
            this.createDbConnection = createDbConnection;
            this.sqlExecutor = sqlExecutor ?? SqlExecutor.Instance;
        }


        #region #0 Schema :  Create

        public override void Create<Entity>()
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));


            string sql = sqlTranslateService.PrepareCreate(entityDescriptor);
            Execute(sql: sql);
        }
        #endregion


        #region #1 Create :  Add AddRange

        public override Entity Add<Entity>(Entity entity)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);

            // #1 prepare sql
            (string sql, Func<object, Dictionary<string, object>> GetSqlParams) = sqlTranslateService.PrepareAdd(arg);

            // #2 get sql params
            var sqlParam = GetSqlParams(entity);

            // #3 execute
            if (entityDescriptor.key.databaseGenerated == System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)
            {
                var keyType = TypeUtil.GetUnderlyingType(entityDescriptor.key.type);
                var newKeyValue = ExecuteScalar(sql: sql, param: sqlParam);
                newKeyValue = TypeUtil.ConvertToUnderlyingType(newKeyValue, keyType);
                if (newKeyValue != null)
                {
                    entityDescriptor.key.SetValue(entity, newKeyValue);
                }
            }
            else
            {
                Execute(sql: sql, param: sqlParam);
            }

            return entity;
        }
        public override void AddRange<Entity>(IEnumerable<Entity> entities)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);

            // #1 prepare sql
            (string sql, Func<object, Dictionary<string, object>> GetSqlParams) = sqlTranslateService.PrepareAdd(arg);

            // #2 execute
            var affectedRowCount = 0;

            if (entityDescriptor.key.databaseGenerated == System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)
            {
                var keyType = TypeUtil.GetUnderlyingType(entityDescriptor.key.type);
                foreach (var entity in entities)
                {
                    var sqlParam = GetSqlParams(entity);
                    var newKeyValue = ExecuteScalar(sql: sql, param: sqlParam);
                    newKeyValue = TypeUtil.ConvertToUnderlyingType(newKeyValue, keyType);
                    if (newKeyValue != null)
                    {
                        entityDescriptor.key.SetValue(entity, newKeyValue);
                    }
                    affectedRowCount++;
                }
            }
            else
            {
                foreach (var entity in entities)
                {
                    var sqlParam = GetSqlParams(entity);
                    Execute(sql: sql, param: sqlParam);
                    affectedRowCount++;
                }
            }

        }

        #endregion



        #region #2 Retrieve : Get Query

        public override Entity Get<Entity>(object keyValue)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);


            // #1 prepare sql
            string sql = sqlTranslateService.PrepareGet(arg);

            // #2 get sql params
            var sqlParam = new Dictionary<string, object>();
            sqlParam[entityDescriptor.keyName] = keyValue;

            // #3 execute
            using var reader = ExecuteReader(sql: sql, param: sqlParam);
            if (reader.Read())
            {
                var entity = (Entity)Activator.CreateInstance(typeof(Entity));
                foreach (var column in entityDescriptor.allColumns)
                {
                    var value = TypeUtil.ConvertToType(reader[column.name], column.type);
                    if (value != null)
                        column.SetValue(entity, value);
                }
                return entity;
            }
            return default;

        }


        public override IQueryable<Entity> Query<Entity>()
        {
            var dbContextId = "SqlDbSet_" + GetHashCode();

            Func<Expression, Type, object> QueryExecutor = (expression, type) =>
            {
                // #1 convert to ExpressionNode
                var isArgument = QueryableBuilder.QueryTypeNameCompare(dbContextId);
                ExpressionNode node = convertService.ConvertToData(expression, autoReduce: true, isArgument: isArgument);
                //var strNode = Json.Serialize(node);


                // #2 convert to Stream
                var stream = StreamReader.ReadNode(node);
                //var strStream = Json.Serialize(stream);


                // #3.1 ExecuteUpdate
                if (stream is StreamToUpdate streamToUpdate)
                {
                    // get arg
                    var resultEntityType = streamToUpdate.fieldsToUpdate.New_GetType();
                    var arg = new QueryTranslateArgument(this, resultEntityType);

                    (string sql, Dictionary<string, object> sqlParam) = sqlTranslateService.PrepareExecuteUpdate(arg, streamToUpdate);

                    return Execute(sql: sql, param: sqlParam);
                }


                // #3.3 Query
                // #3.3.1
                var combinedStream = stream as CombinedStream;
                if (combinedStream == null) combinedStream = new CombinedStream("tmp") { source = stream };

                // #3.3.2 execute and read result
                switch (combinedStream.method)
                {
                    case nameof(Orm_Extensions.ToExecuteString):
                        {
                            // ToExecuteString

                            // get arg
                            var arg = new QueryTranslateArgument(this, null);

                            (string sql, Dictionary<string, object> sqlParam, IDbDataReader dataReader) = sqlTranslateService.PrepareQuery(arg, combinedStream);
                            return sql;
                        }
                    case "Count":
                        {
                            // Count

                            // get arg
                            var arg = new QueryTranslateArgument(this, null);

                            (string sql, Dictionary<string, object> sqlParam, IDbDataReader dataReader) = sqlTranslateService.PrepareQuery(arg, combinedStream);

                            var count = ExecuteScalar(sql: sql, param: sqlParam);
                            return Convert.ToInt32(count);
                        }
                    case nameof(Orm_Extensions.ExecuteDelete):
                        {
                            // ExecuteDelete

                            // get arg
                            var resultEntityType = (combinedStream.source as SourceStream)?.GetEntityType();
                            var arg = new QueryTranslateArgument(this, resultEntityType);

                            (string sql, Dictionary<string, object> sqlParam) = sqlTranslateService.PrepareExecuteDelete(arg, combinedStream);

                            var count = Execute(sql: sql, param: sqlParam);
                            return count;
                        }
                    case "FirstOrDefault" or "First" or "LastOrDefault" or "Last":
                        {
                            // get arg
                            var resultEntityType = expression.Type;
                            var arg = new QueryTranslateArgument(this, resultEntityType);

                            (string sql, Dictionary<string, object> sqlParam, IDbDataReader dataReader) = sqlTranslateService.PrepareQuery(arg, combinedStream);

                            using var reader = ExecuteReader(sql: sql, param: sqlParam);
                            return dataReader.ReadData(reader);
                        }
                    case "ToList":
                    case "":
                    case null:
                        {
                            // ToList

                            // get arg
                            var resultEntityType = expression.Type.GetGenericArguments()?.FirstOrDefault();
                            var arg = new QueryTranslateArgument(this, resultEntityType);

                            (string sql, Dictionary<string, object> sqlParam, IDbDataReader dataReader) = sqlTranslateService.PrepareQuery(arg, combinedStream);

                            using var reader = ExecuteReader(sql: sql, param: sqlParam);
                            return dataReader.ReadData(reader);
                        }
                }
                throw new NotSupportedException("not supported query type: " + combinedStream.method);
            };
            return QueryableBuilder.Build<Entity>(QueryExecutor, dbContextId);

        }

        #endregion



        #region #3 Update: Update UpdateRange

        public override int Update<Entity>(Entity entity)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);

            // #1 prepare sql
            (string sql, Func<object, Dictionary<string, object>> GetSqlParams) = sqlTranslateService.PrepareUpdate(arg);

            // #2 get sql params
            var sqlParam = GetSqlParams(entity);

            // #3 execute
            var affectedRowCount = Execute(sql: sql, param: sqlParam);

            return affectedRowCount;

        }

        public override int UpdateRange<Entity>(IEnumerable<Entity> entities)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);

            // #1 prepare sql
            (string sql, Func<object, Dictionary<string, object>> GetSqlParams) = sqlTranslateService.PrepareUpdate(arg);

            // #2 execute
            var affectedRowCount = 0;

            foreach (var entity in entities)
            {
                var sqlParam = GetSqlParams(entity);
                affectedRowCount += Execute(sql: sql, param: sqlParam);
            }
            return affectedRowCount;
        }

        #endregion


        #region #4 Delete : Delete DeleteRange DeleteByKey DeleteByKeys
        public override int Delete<Entity>(Entity entity)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));

            var key = entityDescriptor.key.GetValue(entity);
            return DeleteByKey<Entity>(key);
        }

        public override int DeleteRange<Entity>(IEnumerable<Entity> entities)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));

            var keys = entities.Select(entity => entityDescriptor.key.GetValue(entity)).ToList();
            return DeleteByKeys<Entity, object>(keys);
        }


        public override int DeleteByKey<Entity>(object keyValue)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);

            // #1 prepare sql
            string sql = sqlTranslateService.PrepareDelete(arg);

            // #2 get sql params
            var sqlParam = new Dictionary<string, object>();
            sqlParam[entityDescriptor.keyName] = keyValue;

            // #3 execute
            var affectedRowCount = Execute(sql: sql, param: sqlParam);

            return affectedRowCount;

        }
        public override int DeleteByKeys<Entity, Key>(IEnumerable<Key> keys)
        {
            // #0 get arg
            var entityDescriptor = GetEntityDescriptor(typeof(Entity));
            SqlTranslateArgument arg = new SqlTranslateArgument(this, entityDescriptor);

            // #1 prepare sql
            (string sql, Dictionary<string, object> sqlParam) = sqlTranslateService.PrepareDeleteByKeys(arg, keys);

            // #2 execute
            var affectedRowCount = Execute(sql: sql, param: sqlParam);
            return affectedRowCount;
        }

        #endregion


        #region Transaction
        public virtual Func<SqlDbContext, ITransactionScope> createTransactionScope { set; get; }
                    = (dbContext) => new SqlTransactionScope(dbContext);
        protected virtual ITransactionScope transactionScope { get; set; }

        public virtual IDbTransaction BeginTransaction()
        {
            transactionScope ??= createTransactionScope(this);
            return transactionScope.BeginTransaction();
        }
        public virtual IDbTransaction GetCurrentTransaction() => transactionScope?.GetCurrentTransaction();

        #endregion



        #region Execute
        protected SqlExecutor sqlExecutor;
        public int? commandTimeout;
        public virtual int ExecuteWithTransaction(string sql, IDictionary<string, object> param = null, IDbTransaction transaction = null)
        {
            commandTimeout ??= this.commandTimeout;
            return sqlExecutor.Execute(dbConnection, sql, param: param, transaction: transaction, commandTimeout: commandTimeout);
        }

        public virtual int Execute(string sql, IDictionary<string, object> param = null, int? commandTimeout = null)
        {
            var transaction = GetCurrentTransaction();
            commandTimeout ??= this.commandTimeout;
            return sqlExecutor.Execute(dbConnection,sql, param: param, transaction: transaction, commandTimeout: commandTimeout);
        }

        public virtual IDataReader ExecuteReader(string sql, IDictionary<string, object> param = null, int? commandTimeout = null)
        {
            var transaction = GetCurrentTransaction();
            commandTimeout ??= this.commandTimeout;
            return sqlExecutor.ExecuteReader(dbConnection, sql, param: param, transaction: transaction, commandTimeout: commandTimeout);
        }

        public virtual object ExecuteScalar(string sql, IDictionary<string, object> param = null, int? commandTimeout = null)
        {
            var transaction = GetCurrentTransaction();
            commandTimeout ??= this.commandTimeout;
            return sqlExecutor.ExecuteScalar(dbConnection, sql, param: param, transaction: transaction, commandTimeout: commandTimeout);
        }
        #endregion

    }
}
