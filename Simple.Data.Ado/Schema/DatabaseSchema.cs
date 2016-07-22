using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Simple.Data.Ado.Schema
{
    public class DatabaseSchema
    {
        private static readonly ConcurrentDictionary<string, DatabaseSchema> Instances = new ConcurrentDictionary<string, DatabaseSchema>();

        private readonly Lazy<TableCollection> _lazyTables;
        private readonly Lazy<ProcedureCollection> _lazyProcedures;
        private readonly Lazy<Operators> _operators;
        private string _defaultSchema;

        private DatabaseSchema(ISchemaProvider schemaProvider, ProviderHelper providerHelper, string defaultSchema = null)
        {
            _lazyTables = new Lazy<TableCollection>(CreateTableCollection);
            _lazyProcedures = new Lazy<ProcedureCollection>(CreateProcedureCollection);
            _operators = new Lazy<Operators>(CreateOperators);
            SchemaProvider = schemaProvider;
            ProviderHelper = providerHelper;
            _defaultSchema = defaultSchema;
        }

        public ProviderHelper ProviderHelper { get; }

        public ISchemaProvider SchemaProvider { get; }

        public bool IsAvailable => SchemaProvider != null;

        public IEnumerable<Table> Tables => _lazyTables.Value.AsEnumerable();

        public bool IsTable(string name)
        {
            try
            {
                var table = FindTable(name);
                return table != null;
            }
            catch (UnresolvableObjectException)
            {
                return false;
            }
        }
        public Table FindTable(string tableName)
        {
            if (!tableName.Contains(".")) return _lazyTables.Value.Find(tableName, DefaultSchema);

            var schemaDotTable = tableName.Split('.');
            return _lazyTables.Value.Find(schemaDotTable[schemaDotTable.Length - 1], schemaDotTable[0], DefaultSchema);
        }

        public Table FindTable(ObjectName tableName)
        {
            return _lazyTables.Value.Find(tableName);
        }

        public Procedure FindProcedure(string procedureName)
        {
            if (!string.IsNullOrWhiteSpace(DefaultSchema) && !(procedureName.Contains(".")))
            {
                procedureName = DefaultSchema + "." + procedureName;
            }
            return _lazyProcedures.Value.Find(procedureName);
        }

        public Procedure FindProcedure(ObjectName procedureName)
        {
            if (string.IsNullOrWhiteSpace(procedureName.Schema) && !string.IsNullOrWhiteSpace(DefaultSchema))
            {
                procedureName = new ObjectName(DefaultSchema, procedureName.Name);
            }
            return _lazyProcedures.Value.Find(procedureName);
        }

        private string DefaultSchema => _defaultSchema ?? (_defaultSchema = SchemaProvider.GetDefaultSchema() ?? string.Empty);

        private TableCollection CreateTableCollection()
        {
            return new TableCollection(SchemaProvider.GetTables()
                .Select(table => new Table(table.ActualName, table.Schema, table.Type, this)));
        }

        private ProcedureCollection CreateProcedureCollection()
        {
            return new ProcedureCollection(SchemaProvider.GetStoredProcedures()
                                                     .Select(
                                                         proc =>
                                                         new Procedure(proc.Name, proc.SpecificName, proc.Schema,
                                                                             this)), SchemaProvider.GetDefaultSchema());
        }

        public string QuoteObjectName(string unquotedName)
        {
            return SchemaProvider.QuoteObjectName(unquotedName);
        }

        public string QuoteObjectName(ObjectName unquotedName)
        {
            if (!string.IsNullOrWhiteSpace(unquotedName.Schema))
                return SchemaProvider.QuoteObjectName(unquotedName.Schema) + '.' + SchemaProvider.QuoteObjectName(unquotedName.Name);
            else
                return SchemaProvider.QuoteObjectName(unquotedName.Name);
        }

        public static DatabaseSchema Get(IConnectionProvider connectionProvider, ProviderHelper providerHelper)
        {
            var schemaConnectionProvider = connectionProvider as ISchemaConnectionProvider;
            var instance = schemaConnectionProvider != null
                ? Instances.GetOrAdd(schemaConnectionProvider.ConnectionString + "#" + schemaConnectionProvider.Schema, sp => new DatabaseSchema(schemaConnectionProvider.GetSchemaProvider(), providerHelper, schemaConnectionProvider.Schema))
                : Instances.GetOrAdd(connectionProvider.ConnectionString, sp => new DatabaseSchema(connectionProvider.GetSchemaProvider(), providerHelper));

            return instance;
        }

        public static void ClearCache()
        {
            Instances.Clear();
        }

        public ObjectName BuildObjectName(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (!text.Contains('.')) return new ObjectName(DefaultSchema, text);
            var schemaDotTable = text.Split('.');
            if (schemaDotTable.Length != 2) throw new InvalidOperationException($"Could not parse table name '{text}'.");
            return new ObjectName(schemaDotTable[0], schemaDotTable[1]);
        }

        public RelationType GetRelationType(string fromTableName, string toTableName)
        {
            var fromTable = FindTable(fromTableName);

            if (fromTable.GetMaster(toTableName) != null) return RelationType.ManyToOne;
            if (fromTable.GetDetail(toTableName) != null) return RelationType.OneToMany;
            return RelationType.None;
        }

        public bool IsProcedure(string procedureName)
        {
            return _lazyProcedures.Value.IsProcedure(procedureName);
        }

        public Operators Operators => _operators.Value;

        private Operators CreateOperators()
        {
            return ProviderHelper.GetCustomProvider<Operators>(SchemaProvider) ?? new Operators();
        }
    }

    public enum RelationType
    {
        None,
        OneToMany,
        ManyToOne
    }
}