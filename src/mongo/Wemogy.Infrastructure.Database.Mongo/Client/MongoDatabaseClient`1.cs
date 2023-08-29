using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using SortDirection = Wemogy.Infrastructure.Database.Core.Enums.SortDirection;

namespace Wemogy.Infrastructure.Database.Mongo.Client
{
    public class MongoDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : class
    {
        private readonly IMongoCollection<TEntity> _collection;
        private readonly ILogger? _logger;

        private readonly string _idProperty;
        private readonly string _partitionKeyProperty;

        public MongoDatabaseClient(MongoClient mongoClient, MongoDatabaseClientOptions options, ILogger? logger)
        {
            _collection = mongoClient
                .GetDatabase(options.DatabaseName)
                .GetCollection<TEntity>(options.ContainerName);
            _logger = logger;

            var idPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<IdAttribute>();
            if (idPropertyInfo == null)
            {
                throw Error.Unexpected(
                    "IdPropertyNotFound",
                    $"Entity {typeof(TEntity).Name} does not have a property with IdAttribute");
            }

            _idProperty = idPropertyInfo.Name;

            var partitionKeyPropertyInfo = typeof(TEntity).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
            if (partitionKeyPropertyInfo == null)
            {
                throw Error.Unexpected(
                    "PartitionKeyPropertyNotFound",
                    $"Entity {typeof(TEntity).Name} does not have a property with PartitionKeyAttribute");
            }

            _partitionKeyProperty = partitionKeyPropertyInfo.Name;
        }

        public async Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            var filter = GetEntityFilterDefinition(
                id,
                partitionKey);
            var entity = await _collection.Find(filter)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKey);
            }

            return entity;
        }

        public async Task IterateAsync(
            QueryParameters queryParameters,
            Expression<Func<TEntity, bool>>? generalFilterPredicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var filterDefinition = GetEntityFilterDefinition(queryParameters);

            if (generalFilterPredicate != null)
            {
                filterDefinition &= Builders<TEntity>.Filter.Where(generalFilterPredicate);
            }

            var options = new FindOptions<TEntity>()
            {
                Limit = queryParameters.Take
            };

            var entities = await _collection.FindAsync(
                filterDefinition,
                options,
                cancellationToken);

            while (await entities.MoveNextAsync(cancellationToken))
            {
                foreach (var entity in entities.Current)
                {
                    await callback(entity);
                }
            }
        }

        public async Task IterateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Sorting<TEntity>? sorting,
            PaginationParameters? paginationParameters,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var options = new FindOptions<TEntity>()
            {
                Limit = paginationParameters?.Take,
                Skip = paginationParameters?.Skip
            };

            if (sorting != null && sorting.Parameters.Any())
            {
                BsonDocument sortDefinition = new BsonDocument();

                foreach (var sortingParameter in sorting.Parameters)
                {
                    sortDefinition.Add(
                        sortingParameter.CamelCaseProperty,
                        sortingParameter.Direction == SortDirection.Ascending ? 1 : -1);
                }

                options.Sort = new BsonDocumentSortDefinition<TEntity>(sortDefinition);
            }

            var entities = await _collection.FindAsync(predicate, options, cancellationToken);
            while (await entities.MoveNextAsync(cancellationToken))
            {
                foreach (var entity in entities.Current)
                {
                    await callback(entity);
                }
            }
        }

        public Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
        {
            return _collection.CountDocumentsAsync(
                predicate,
                cancellationToken: cancellationToken);
        }

        public async Task<TEntity> CreateAsync(TEntity entity)
        {
            try
            {
                await _collection.InsertOneAsync(entity);
                return entity;
            }
            catch (MongoWriteException e)
            {
                if (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    throw Error.Conflict(
                        "AlreadyExists",
                        $"Entity with id {ResolveIdValue(entity)} already exists");
                }

                throw;
            }
        }

        public async Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var filter = GetEntityFilterDefinition(entity);
            var result = await _collection.FindOneAndReplaceAsync(
                filter,
                entity);

            if (result == null)
            {
                throw DatabaseError.EntityNotFound(
                    ResolveIdValue(entity),
                    ResolvePartitionKeyValue(entity));
            }

            return entity;
        }

        public async Task DeleteAsync(string id, string partitionKey)
        {
            var filter = GetEntityFilterDefinition(
                id,
                partitionKey);
            var result = await _collection.DeleteOneAsync(filter);

            if (result.DeletedCount == 0)
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKey);
            }
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return _collection.DeleteManyAsync(predicate);
        }

        private FilterDefinition<TEntity> GetEntityFilterDefinition(string id, string partitionKey)
        {
            return Builders<TEntity>.Filter.Eq(_idProperty, id) &
                        Builders<TEntity>.Filter.Eq(_partitionKeyProperty, partitionKey);
        }

        private FilterDefinition<TEntity> GetEntityFilterDefinition(TEntity entity)
        {
            return GetEntityFilterDefinition(
                ResolveIdValue(entity),
                ResolvePartitionKeyValue(entity));
        }

        private FilterDefinition<TEntity> GetEntityFilterDefinition(QueryParameters queryParameters)
        {
            var filterDefinition = FilterDefinition<TEntity>.Empty;
            foreach (var queryFilter in queryParameters.Filters)
            {
                filterDefinition &= GetEntityFilterDefinition(queryFilter);
            }

            return filterDefinition;
        }

        private FilterDefinition<TEntity> GetEntityFilterDefinition(QueryFilter queryFilter)
        {
            var propertyName = queryFilter.Property.UpperFirst();
            var propertyType = typeof(TEntity).GetProperty(propertyName)?.PropertyType;

            if (propertyType == null)
            {
                throw Error.Unexpected(
                    "PropertyNotFound",
                    $"Property {propertyName} not found on entity {typeof(TEntity).Name}");
            }

            var value = queryFilter.Value.FromJson(propertyType);
            switch (queryFilter.Comparator)
            {
                case Comparator.Equals:
                    return Builders<TEntity>.Filter.Eq(propertyName, value);
                case Comparator.NotEquals:
                    return Builders<TEntity>.Filter.Ne(propertyName, value);
                case Comparator.Contains:
                    return Builders<TEntity>.Filter.Regex(propertyName, new BsonRegularExpression(value as string));
                case Comparator.ContainsIgnoreCase:
                    // i flag indicates a case-insensitive search
                    return Builders<TEntity>.Filter.Regex(propertyName, new BsonRegularExpression(value as string, "i"));
                case Comparator.StartsWith:
                    return Builders<TEntity>.Filter.Regex(propertyName, new BsonRegularExpression($"^{value}"));
                case Comparator.StartsWithIgnoreCase:
                    // i flag indicates a case-insensitive search
                    return Builders<TEntity>.Filter.Regex(propertyName, new BsonRegularExpression($"^{value}", "i"));
                default:
                    throw new NotImplementedException($"The comparator {queryFilter.Comparator} is not implemented. Feel free to implement it or create an issue.");
            }
        }
    }
}
