﻿using Vitorm.DataProvider;
using Vitorm.Sql;

namespace Vit.Extensions.Vitorm_Extensions
{
    public static class IDataProvider_Extensions
    {
        public static SqlDbContext CreateSqlDbContext(this IDataProvider dataProvider)
        {
            return (dataProvider as SqlDataProvider)?.CreateDbContext();
        }

    }
}