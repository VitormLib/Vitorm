﻿using System;
using System.Linq;

namespace Vitorm.Entity.Loader.DataAnnotations
{
    public partial class EntityDescriptor : IEntityDescriptor
    {
        public EntityDescriptor(Type entityType, IColumnDescriptor[] allColumns, string tableName, string schema = null)
        {
            this.entityType = entityType;
            this.tableName = tableName;
            this.schema = schema;

            this.key = allColumns.FirstOrDefault(m => m.isKey);
            this.columns = allColumns.Where(m => !m.isKey).OrderBy(col => col.columnOrder ?? int.MaxValue).ToArray();
            this.allColumns = allColumns.OrderBy(col => col.columnOrder ?? int.MaxValue).ToArray();
        }


        public Type entityType { get; protected set; }
        public string tableName { get; protected set; }
        public string schema { get; protected set; }

        /// <summary>
        /// primary key name
        /// </summary>
        public string keyName => key?.columnName;

        /// <summary>
        /// primary key
        /// </summary>
        public IColumnDescriptor key { get; protected set; }

        /// <summary>
        /// not include primary key
        /// </summary>
        public IColumnDescriptor[] columns { get; protected set; }


        public IColumnDescriptor[] allColumns { get; protected set; }


    }
}
