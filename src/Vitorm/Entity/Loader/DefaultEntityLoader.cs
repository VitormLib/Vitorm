﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Vitorm.Entity.LoaderAttribute;

namespace Vitorm.Entity.Loader
{
    public class DefaultEntityLoader : IEntityLoader
    {
        public List<IEntityLoader> loaders = new();
        public DefaultEntityLoader()
        {
            loaders.Add(new DataAnnotations.EntityLoader());

            loaders.Add(new EntityLoaderFromAttribute());
        }

        readonly ConcurrentDictionary<Type, IEntityDescriptor> descriptorCache = new();

        public void CleanCache()
        {
            descriptorCache.Clear();
            foreach (var loader in loaders) loader.CleanCache();
        }

        public IEntityDescriptor LoadDescriptor(Type entityType)
        {
            if (descriptorCache.TryGetValue(entityType, out var entityDescriptor)) return entityDescriptor;
            entityDescriptor = LoadDescriptorWithoutCache(entityType);
            if (entityDescriptor != null)
                descriptorCache[entityType] = entityDescriptor;
            return entityDescriptor;
        }

        public IEntityDescriptor LoadDescriptorWithoutCache(Type entityType)
        {
            foreach (var loader in loaders)
            {
                var entityDescriptor = loader.LoadDescriptor(entityType);
                if (entityDescriptor != null)
                {
                    return entityDescriptor;
                }
            }
            return null;
        }

    }
}
