﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Vit.Extensions.Linq_Extensions;
using Vit.Linq.ExpressionTree.ComponentModel;
using Vitorm.Entity;
using Vitorm.Sql;
using Vitorm.Sql.SqlTranslate;
using Vitorm.MySql.TranslateService;
using System.ComponentModel.DataAnnotations.Schema;
using Vitorm.StreamQuery;

namespace Vitorm.MySql
{
    public class SqlTranslateService : Vitorm.Sql.SqlTranslate.SqlTranslateService
    {
        public static readonly SqlTranslateService Instance = new SqlTranslateService();

        protected QueryTranslateService queryTranslateService;
        protected ExecuteUpdateTranslateService executeUpdateTranslateService;
        protected ExecuteDeleteTranslateService executeDeleteTranslateService;

        public SqlTranslateService()
        {
            queryTranslateService = new QueryTranslateService(this);
            executeUpdateTranslateService = new ExecuteUpdateTranslateService(this);
            executeDeleteTranslateService = new ExecuteDeleteTranslateService(this);
        }
        /// <summary>
        ///     Generates the delimited SQL representation of an identifier (column name, table name, etc.).
        /// </summary>
        /// <param name="identifier">The identifier to delimit.</param>
        /// <returns>
        ///     The generated string.
        /// </returns>
        public override string DelimitIdentifier(string identifier) => $"`{EscapeIdentifier(identifier)}`"; // Interpolation okay; strings

        /// <summary>
        ///     Generates the escaped SQL representation of an identifier (column name, table name, etc.).
        /// </summary>
        /// <param name="identifier">The identifier to be escaped.</param>
        /// <returns>
        ///     The generated string.
        /// </returns>
        public override string EscapeIdentifier(string identifier) => identifier.Replace("`", "\\`");


        #region EvalExpression
        /// <summary>
        /// read where or value or on
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <param name="data"></param>
        public override string EvalExpression(QueryTranslateArgument arg, ExpressionNode data)
        {
            switch (data.nodeType)
            {
                case NodeType.MethodCall:
                    {
                        ExpressionNode_MethodCall methodCall = data;
                        switch (methodCall.methodName)
                        {
                            // ##1 ToString
                            case nameof(object.ToString):
                                {
                                    return $"cast({EvalExpression(arg, methodCall.@object)} as char)";
                                }

                            #region ##2 String method:  StartsWith EndsWith Contains
                            case nameof(string.StartsWith): // String.StartsWith
                                {
                                    var str = methodCall.@object;
                                    var value = methodCall.arguments[0];
                                    return $"{EvalExpression(arg, str)} like concat({EvalExpression(arg, value)},'%')";
                                }
                            case nameof(string.EndsWith): // String.EndsWith
                                {
                                    var str = methodCall.@object;
                                    var value = methodCall.arguments[0];
                                    return $"{EvalExpression(arg, str)} like concat('%',{EvalExpression(arg, value)})";
                                }
                            case nameof(string.Contains) when methodCall.methodCall_typeName == "String": // String.Contains
                                {
                                    var str = methodCall.@object;
                                    var value = methodCall.arguments[0];
                                    return $"{EvalExpression(arg, str)} like concat('%',{EvalExpression(arg, value)},'%')";
                                }
                                #endregion
                        }
                        break;
                    }

                #region Read Value
                case NodeType.Convert:
                    {
                        // cast( 4.1 as signed)

                        ExpressionNode_Convert convert = data;

                        Type targetType = convert.valueType?.ToType();

                        if (targetType == typeof(object)) return EvalExpression(arg, convert.body);

                        // Nullable
                        if (targetType.IsGenericType) targetType = targetType.GetGenericArguments()[0];

                        string targetDbType = GetDbType(targetType);

                        var sourceType = convert.body.Member_GetType();
                        if (sourceType != null)
                        {
                            if (sourceType.IsGenericType) sourceType = sourceType.GetGenericArguments()[0];

                            if (targetDbType == GetDbType(sourceType)) return EvalExpression(arg, convert.body);
                        }

                        if (targetType == typeof(string))
                        {
                            return $"cast({EvalExpression(arg, convert.body)} as char)";
                        }

                        return $"cast({EvalExpression(arg, convert.body)} as {targetDbType})";
                    }
                case nameof(ExpressionType.Add):
                    {
                        ExpressionNode_Binary binary = data;

                        // ##1 String Add
                        if (data.valueType?.ToType() == typeof(string))
                        {
                            return $"CONCAT({EvalExpression(arg, binary.left)} ,{EvalExpression(arg, binary.right)})";
                        }

                        // ##2 Numberic Add
                        return $"{EvalExpression(arg, binary.left)} + {EvalExpression(arg, binary.right)}";
                    }
                case nameof(ExpressionType.Coalesce):
                    {
                        ExpressionNode_Binary binary = data;
                        return $"COALESCE({EvalExpression(arg, binary.left)},{EvalExpression(arg, binary.right)})";
                    }
                    #endregion

            }

            return base.EvalExpression(arg, data);
        }
        #endregion



        #region PrepareCreate
        public override string PrepareCreate(IEntityDescriptor entityDescriptor)
        {
            /* //sql
CREATE TABLE user (
  id int NOT NULL PRIMARY KEY,
  name varchar(100) DEFAULT NULL,
  birth date DEFAULT NULL,
  fatherId int DEFAULT NULL,
  motherId int DEFAULT NULL
) ;
              */
            List<string> sqlFields = new();

            // #1 primary key
            sqlFields.Add(GetColumnSql(entityDescriptor.key) + " PRIMARY KEY " + (entityDescriptor.key.databaseGenerated == DatabaseGeneratedOption.Identity ? "AUTO_INCREMENT " : ""));

            // #2 columns
            entityDescriptor.columns?.ForEach(column => sqlFields.Add(GetColumnSql(column)));

            return $@"
CREATE TABLE {DelimitTableName(entityDescriptor)} (
{string.Join(",\r\n  ", sqlFields)}
)";


            string GetColumnSql(IColumnDescriptor column)
            {
                var dbType = column.databaseType ?? GetDbType(column.type);
                // name varchar(100) DEFAULT NULL
                return $"  {DelimitIdentifier(column.name)} {dbType} {(column.nullable ? "DEFAULT NULL" : "NOT NULL")}";
            }
        }
        protected override string GetDbType(Type type)
        {
            type = TypeUtil.GetUnderlyingType(type);

            if (type == typeof(DateTime))
                return "DATETIME";

            if (type == typeof(string))
                return "varchar(1000)";

            if (type == typeof(float)) return "FLOAT";
            if (type == typeof(double) || type == typeof(decimal))
                return "DOUBLE";

            if (type == typeof(Int32)) return "INTEGER";
            if (type == typeof(Int16)) return "SMALLINT";
            if (type == typeof(byte)) return "TINYINT";
            if (type == typeof(bool)) return "TINYINT";

            if (type.Name.ToLower().Contains("int")) return "INTEGER";

            throw new NotSupportedException("unsupported column type:" + type.Name);
        }
        #endregion


        public override (string sql, Func<object, Dictionary<string, object>> GetSqlParams) PrepareAdd(SqlTranslateArgument arg)
        {
            /* //sql
             insert into user(name,birth,fatherId,motherId) values('','','');
             select seq from sqlite_sequence where name='user';
              */
            var entityDescriptor = arg.entityDescriptor;

            var columns = entityDescriptor.columns;

            // #1 GetSqlParams 
            Func<object, Dictionary<string, object>> GetSqlParams = (entity) =>
            {
                var sqlParam = new Dictionary<string, object>();
                foreach (var column in columns)
                {
                    var columnName = column.name;
                    var value = column.GetValue(entity);

                    sqlParam[columnName] = value;
                }
                return sqlParam;
            };

            #region #2 columns 
            List<string> columnNames = new List<string>();
            List<string> valueParams = new List<string>();
            string columnName;

            foreach (var column in columns)
            {
                columnName = column.name;

                columnNames.Add(DelimitIdentifier(columnName));
                valueParams.Add(GenerateParameterName(columnName));
            }
            #endregion

            // #3 build sql
            string sql = $@"insert into {DelimitTableName(entityDescriptor)}({string.Join(",", columnNames)}) values({string.Join(",", valueParams)});";
            sql += "select last_insert_id();";
            return (sql, GetSqlParams);
        }

        public override (string sql, Dictionary<string, object> sqlParam, IDbDataReader dataReader) PrepareQuery(QueryTranslateArgument arg, CombinedStream combinedStream)
        {
            string sql = queryTranslateService.BuildQuery(arg, combinedStream);
            return (sql, arg.sqlParam, arg.dataReader);
        }

        public override (string sql, Dictionary<string, object> sqlParam) PrepareExecuteUpdate(QueryTranslateArgument arg, CombinedStream combinedStream)
        {
            string sql = executeUpdateTranslateService.BuildQuery(arg, combinedStream);
            return (sql, arg.sqlParam);
        }

        public override (string sql, Dictionary<string, object> sqlParam) PrepareExecuteDelete(QueryTranslateArgument arg, CombinedStream combinedStream)
        {
            string sql = executeDeleteTranslateService.BuildQuery(arg, combinedStream);
            return (sql, arg.sqlParam);
        }



    }
}
