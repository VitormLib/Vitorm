﻿using System.Data;

using Vitorm.Sql;
using Vitorm.Sql.Transaction;

using SqlTransaction = MySqlConnector.MySqlTransaction;

namespace Vitorm.MySql
{
    public class SqlTransactionScope : Vitorm.Sql.Transaction.SqlTransactionScope
    {
        int savePointCount = 0;
        public DbTransactionWrap CreateTransactionSavePoint(IDbTransaction originalTransaction)
        {
            var savePointName = "tran" + savePointCount++;
            return new DbTransactionWrapSavePoint(originalTransaction, savePointName);
        }
        public SqlTransactionScope(SqlDbContext dbContext) : base(dbContext)
        {
        }

        public override IDbTransaction BeginTransaction()
        {
            DbTransactionWrap transactionWrap;
            IDbTransaction originalTransaction = GetCurrentTransaction();
            if (originalTransaction == null)
            {
                var dbConnection = dbContext.dbConnection as MySqlConnector.MySqlConnection;
                if (dbConnection.State != ConnectionState.Open) dbConnection.Open();

                originalTransaction = dbConnection.BeginTransaction();
                dbContext.ExecuteWithTransaction("SET autocommit=0;", transaction: originalTransaction);
                transactionWrap = new DbTransactionWrap(originalTransaction);
            }
            else
            {
                transactionWrap = CreateTransactionSavePoint(originalTransaction);
            }

            transactions.Push(transactionWrap);
            return transactionWrap;
        }



        public class DbTransactionWrapSavePoint : DbTransactionWrap
        {
            public SqlTransaction sqlTran => (SqlTransaction)originalTransaction;
            readonly string savePointName;
            public DbTransactionWrapSavePoint(IDbTransaction transaction, string savePointName) : base(transaction)
            {
                this.savePointName = savePointName;
                sqlTran.Save(savePointName);
            }

            public override void Commit()
            {
                sqlTran.Release(savePointName);
                TransactionState = ETransactionState.Committed;
            }

            public override void Dispose()
            {
                if (TransactionState == ETransactionState.Active)
                    sqlTran.Rollback(savePointName);
                TransactionState = ETransactionState.Disposed;
            }

            public override void Rollback()
            {
                sqlTran.Rollback(savePointName);
                TransactionState = ETransactionState.RolledBack;
            }
        }
    }
}