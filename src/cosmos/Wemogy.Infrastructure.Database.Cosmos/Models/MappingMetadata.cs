using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class MappingMetadata
    {
        private readonly Dictionary<string, Type> _propertyTypes;

        public MappingMetadata()
        {
            _propertyTypes = new Dictionary<string, Type>(StringComparer.CurrentCultureIgnoreCase);
        }

        public void InitializeUsingReflection(Type modelType)
        {
            // ToDo: implement somehow
        }

        public void AddCustomMappings(Dictionary<string, Type> customMappings)
        {
            this._propertyTypes.Merge(customMappings);
        }

        public object Deserialize(string propertyPath, string jsonValue)
        {
            try
            {
                var deserializedValue = JsonConvert.DeserializeObject(jsonValue);
                if (deserializedValue == null)
                {
                    return null;
                }

                if (this._propertyTypes.TryGetValue(propertyPath, out Type propertyType))
                {
                    if (propertyType == typeof(DateTime))
                    {
                        if (deserializedValue is long l)
                        {
                            return l.FromUnixEpochDate();
                        }
                    }
                }

                return deserializedValue;
            }
            catch
            {
                Console.WriteLine($"MappingMetadata.Deserialize: Use fallback for property {propertyPath} with json value {jsonValue}");
                return jsonValue;
            }
        }
    }
}
