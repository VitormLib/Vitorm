﻿using System;
using System.Collections.Generic;
using System.Linq;

using Vitorm.Sql.SqlTranslate;
using Vitorm.StreamQuery;

namespace Vitorm.MySql.TranslateService
{
    public class ExecuteUpdateTranslateService : BaseQueryTranslateService
    {
        /*

-- multiple
WITH tmp AS (
    select  concat('u' , cast(u.id as char) , '_' , COALESCE(cast(father.id as char),'') ) as name , u.id 
    from `User` u
    left join `User` father on u.fatherId = father.id 
    where u.id > 0
)
UPDATE `User` t0,tmp
  SET t0.name =  tmp.name
where t0.id = tmp.id ;
         */
        public override string BuildQuery(QueryTranslateArgument arg, CombinedStream stream)
        {
            var sqlInner = base.BuildQuery(arg, stream);

            var entityDescriptor = arg.dbContext.GetEntityDescriptor(arg.resultEntityType);
            var columnsToUpdate = (stream as StreamToUpdate)?.fieldsToUpdate?.memberArgs;

            var NewLine = "\r\n";
            var keyName = entityDescriptor.keyName;


            var sql = $"WITH tmp AS ( {NewLine}";
            sql += sqlInner;

            sql += $"{NewLine}){NewLine}";
            sql += $"UPDATE {sqlTranslator.DelimitTableName(entityDescriptor)} t0, tmp{NewLine}";
            sql += $"Set ";

            var sqlToUpdateCols = columnsToUpdate
                .Select(m => m.name)
                .Select(name =>
                {
                    var columnName = entityDescriptor.GetColumnNameByPropertyName(name);
                    return $"{NewLine}  {sqlTranslator.GetSqlField("t0", columnName)} = {sqlTranslator.GetSqlField("tmp", name)} ";
                });

            sql += string.Join(",", sqlToUpdateCols);

            sql += $"{NewLine}where {sqlTranslator.GetSqlField("t0", keyName)}={sqlTranslator.GetSqlField("tmp", keyName)} ";

            return sql;
        }


        public ExecuteUpdateTranslateService(SqlTranslateService sqlTranslator) : base(sqlTranslator)
        {
        }

        protected override string ReadSelect(QueryTranslateArgument arg, CombinedStream stream, string prefix = "select")
        {
            var entityDescriptor = arg.dbContext.GetEntityDescriptor(arg.resultEntityType);
            var columnsToUpdate = (stream as StreamToUpdate)?.fieldsToUpdate?.memberArgs;

            if (columnsToUpdate?.Any() != true) throw new ArgumentException("can not get columns to update");

            var sqlFields = new List<string>();

            foreach (var column in columnsToUpdate)
            {
                sqlFields.Add($"({sqlTranslator.EvalExpression(arg, column.value)}) as {sqlTranslator.DelimitIdentifier(column.name)}");
            }
            // primary key
            sqlFields.Add($"{sqlTranslator.GetSqlField(stream.source.alias, entityDescriptor.keyName)} as {sqlTranslator.DelimitIdentifier(entityDescriptor.keyName)}");

            return prefix + " " + String.Join(",", sqlFields);
        }



    }
}
