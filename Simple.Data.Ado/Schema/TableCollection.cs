using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Simple.Data.Extensions;

namespace Simple.Data.Ado.Schema
{
    class TableCollection : Collection<Table>
    {
        public TableCollection() { }

        public TableCollection(IEnumerable<Table> tables)
            : base(tables.ToList())
        {
        }

        /// <summary>
        ///     Finds the Table with a name most closely matching the specified table name, in one of the specified schemas.
        ///     This method will try an exact match first, then a case-insensitve search, then a pluralized or singular version.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schemaNames">The schemas to check for the table, in order checked.</param>
        /// <exception cref="UnresolvableObjectException"></exception>
        /// <returns>A <see cref="Table"/> if a match is found; otherwise, <c>null</c>.</returns>
        public Table Find(string tableName, params string[] schemaNames)
        {
            Table table = null;

            foreach (var schemaName in schemaNames)
            {
                table = find(tableName, schemaName);
                if (table != null) break; //Found one, break out of the loop
            }

            if (table == null) //still nothing? check without the schema.
            {
                table = find(tableName);
            }

            ////Check the given schema (if one is embedded in the tableName)
            //if (tableName.Contains('.'))
            //{
            //    var schemaDotTable = tableName.Split('.');

            //    table = find(schemaDotTable[schemaDotTable.Length - 1], schemaDotTable[0]);
            //}

            ////Check the schema passed in
            //if (table == null && !string.IsNullOrEmpty(schemaName))
            //{
            //    if (tableName.Contains('.'))
            //    {
            //        var schemaDotTable = tableName.Split('.');
            //        table = find(schemaDotTable[schemaDotTable.Length - 1], schemaName);
            //    }
            //    else
            //    {
            //        table = find(tableName, schemaName);
            //    }
            //}

            ////Check the given table name
            //if (table == null)
            //{
            //    table = FindTableWithName(tableName.Homogenize())
            //            ?? FindTableWithPluralName(tableName.Homogenize())
            //            ?? FindTableWithSingularName(tableName.Homogenize());
            //}

            if (table == null)
            {
                throw new UnresolvableObjectException(tableName,
                    $"Table \'{tableName}\' not found{(schemaNames.Any() ? " in Schemas " + string.Join(", ", schemaNames) : "")}, or insufficient permissions.");
            }

            return table;
        }

        /// <summary>
        /// Finds the Table with a name most closely matching the specified table name.
        /// This method will try an exact match first, then a case-insensitve search, then a pluralized or singular version.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="schemaName"></param>
        /// <returns>A <see cref="Table"/> if a match is found; otherwise, <c>null</c>.</returns>
        private Table find(string tableName, string schemaName = null)
        {
            Table table;
            if (!string.IsNullOrEmpty(schemaName))
            {
                table = FindTableWithName(tableName.Homogenize(), schemaName.Homogenize())
                            ?? FindTableWithPluralName(tableName.Homogenize(), schemaName.Homogenize())
                            ?? FindTableWithSingularName(tableName.Homogenize(), schemaName.Homogenize());
            }
            else
            {
                table = FindTableWithName(tableName.Homogenize())
                        ?? FindTableWithPluralName(tableName.Homogenize())
                        ?? FindTableWithSingularName(tableName.Homogenize());
            }

            //if (table == null)
            //{
            //    string fullTableName = schemaName + '.' + tableName;
            //    throw new UnresolvableObjectException(fullTableName, string.Format("Table '{0}' not found, or insufficient permissions.", fullTableName));
            //}

            return table;
        }

        private Table FindTableWithSingularName(string tableName, string schemaName)
        {
            return FindTableWithName(tableName.Singularize(), schemaName);
        }

        private Table FindTableWithPluralName(string tableName, string schemaName)
        {
            return FindTableWithName(tableName.Pluralize(), schemaName);
        }

        private Table FindTableWithName(string tableName, string schemaName)
        {
            var tables = this
                .Where(t => t.HomogenizedName.Equals(tableName) && (t.Schema == null || t.Schema.Homogenize().Equals(schemaName)));
            return tables.Count() == 1 ? tables.Single() : null;
        }

        private Table FindTableWithName(string tableName)
        {
            var tables = this.Where(t => t.HomogenizedName.Equals(tableName));
            return tables.Count() == 1 ? tables.Single() : null;
        }

        private Table FindTableWithPluralName(string tableName)
        {
            return FindTableWithName(tableName.Pluralize());
        }

        private Table FindTableWithSingularName(string tableName)
        {
            if (tableName.IsPlural())
            {
                return FindTableWithName(tableName.Singularize());
            }

            return null;
        }

        public Table Find(ObjectName tableName)
        {
            return Find(tableName.Name, tableName.Schema);
        }
    }
}
