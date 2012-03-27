﻿namespace SignalR.Hubs.Lookup
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using SignalR.Hubs.Lookup.Descriptors;
    using SignalR.Infrastructure;

    /// <summary>
    /// Default hub descriptor provider. Finds all IHub-implementing types and lists them as hubs.
    /// </summary>
    public class ReflectedHubDescriptorProvider: IHubDescriptorProvider
    {
        private readonly Lazy<IDictionary<string, HubDescriptor>> _hubs;

        public ReflectedHubDescriptorProvider()
        {
            this._hubs = new Lazy<IDictionary<string, HubDescriptor>>(BuildHubsCache);
        }

        public IEnumerable<HubDescriptor> GetHubs()
        {
            return this._hubs.Value.Select(kv => kv.Value).Distinct();
        }

        public bool TryGetHub(string hubName, out HubDescriptor descriptor)
        {
            return _hubs.Value.TryGetValue(hubName, out descriptor);
        }

        protected IDictionary<string, HubDescriptor> BuildHubsCache()
        {
            return GetAssemblies()
                .Where(a => !a.GlobalAssemblyCache && !a.IsDynamic)
                .SelectMany(GetTypesSafe)
                .Where(type => typeof(IHub).IsAssignableFrom(type) && !type.IsAbstract)
                .Select(type => new HubDescriptor
                    {
                        Name = ReflectionHelper.GetAttributeValue<HubNameAttribute, string>(type, attr => attr.HubName) ?? type.Name,
                        Type = type
                    })
                .SelectMany(x => CacheKeysFor(x.Type).Select(key => new { Descriptor = x, Key = key }))
                .ToDictionary(xx => xx.Key, xx => xx.Descriptor);
        }

        protected virtual IEnumerable<Assembly> GetAssemblies()
        {
            // TODO: Look for a better default, chances are the hubs aren't even loaded yet
            return AppDomain.CurrentDomain.GetAssemblies();
        }

        private static IEnumerable<string> CacheKeysFor(Type type)
        {
            yield return type.FullName;
            yield return type.Name;

            var attributeName = ReflectionHelper.GetAttributeValue<HubNameAttribute, string>(type, attr => attr.HubName);
            if (attributeName != null && !String.Equals(attributeName, type.Name, StringComparison.OrdinalIgnoreCase))
            {
                yield return attributeName;
            }
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly a)
        {
            try
            {
                return a.GetTypes();
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}