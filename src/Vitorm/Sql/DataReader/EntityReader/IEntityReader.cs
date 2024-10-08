﻿using System;
using System.Data;

using Vit.Linq.ExpressionNodes.ComponentModel;

namespace Vitorm.Sql.DataReader.EntityReader
{
    public interface IEntityReader
    {
        void Init(EntityReaderConfig config, Type entityType, ExpressionNode resultSelector);
        object ReadEntity(IDataReader reader);

    }
}
