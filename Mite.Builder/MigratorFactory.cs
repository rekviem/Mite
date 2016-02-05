﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mite.Core;
using Newtonsoft.Json.Linq;

namespace Mite.Builder {
    public static class MigratorFactory {
        public static Migrator GetMigratorFromConfig(string config, string directoryPath) {
            var databaseRepositories = InstancesOf<IDatabaseRepository>();
            var options = JObject.Parse(config);
            var repoName = options.Value<string>("repositoryName");
            var connString = options.Value<string>("connectionString");

            if (string.IsNullOrEmpty(repoName)) {
                throw new Exception("Invalid Config - repositoryName is required.");
            }
            if (string.IsNullOrEmpty(connString)) {
                throw new Exception("Invalid Config - connectionString is required.");
            }
            object[] args = new object[]{connString, directoryPath};

            IDatabaseRepository databaseRepository = null;

            foreach (var repoType in databaseRepositories)
            {
                if (repoName.ToLower() == repoType.Name.ToLower())
                {
                    databaseRepository = (IDatabaseRepository)Activator.CreateInstance(repoType, BindingFlags.CreateInstance, null, args, null);
                    break;
                }                    
            }

            if (databaseRepository == null)
            {
                try
                {
                    Type repoType = Type.GetType(repoName);
                    databaseRepository = (IDatabaseRepository)Activator.CreateInstance(repoType, BindingFlags.CreateInstance, null, args, null);
                }
                catch (TypeLoadException ex)
                {
                    throw new Exception("Could not load repository: " + repoName);
                }
            }
            
            var miteDb = databaseRepository.Create();
            return new Migrator(miteDb, databaseRepository);
        }

        public static Migrator GetMigrator(string directoryName) {
            var miteConfigPath = Path.Combine(directoryName, "mite.config");
            if (File.Exists(miteConfigPath)) {
                return GetMigratorFromConfig(File.ReadAllText(miteConfigPath), directoryName);
            } else {
                throw new FileNotFoundException("mite.config is not contained in the directory specified");
            }
        }

        public static Migrator GetMigrator(IDatabaseRepository databaseRepository, string directoryPath)
        {
            var tracker = databaseRepository.Create();
            return new Migrator(tracker, databaseRepository);
        }

        private static IEnumerable<Type> InstancesOf<T>()
        {
            var executingDirectory= Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var files = Directory.GetFiles(executingDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            var type = typeof(T);
            return files.Select(Assembly.LoadFrom).SelectMany(a => (from t in a.GetExportedTypes()
                                                                            where t.IsClass
                                                                                  && type.IsAssignableFrom(t)
                                                                            select t));
        }
    }
}
