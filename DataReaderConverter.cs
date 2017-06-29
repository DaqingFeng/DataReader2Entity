using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Common
{
    public class DataReaderConverter
    {
        //a dictionary of all properties on the entity
        private static ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberInfo>> classProperty = new ConcurrentDictionary<Type, ConcurrentDictionary<string, MemberInfo>>();

        /// <summary>
        /// Reads data reader into a list of entities
        /// </summary>
        /// <param name="dataReader">The data reader.</param>
        /// <returns>A list of the specified entities</returns>
        public static IList<TEntity> Fill<TEntity>(IDataReader dataReader) where TEntity : new() //must have public parameterless constructor
        {
            int filedcount = dataReader.FieldCount;
            if (filedcount == 0) throw new ArgumentNullException("None of filed be queried");
            //a list of dictionaries for each row
            var rows = new List<IDictionary<string, object>>();
            while (dataReader.Read())
            {
                rows.Add(ReadRow(dataReader, 0, filedcount));
            }
            //close the dataReader
            dataReader.Close();
            if (rows.Count == 0)
            {
                return default(IList<TEntity>);
            }
            IList<TEntity> result = new List<TEntity>();
            //use the list of dictionaries
            foreach (var row in rows)
            {
                var entity = BuildEntity<TEntity>(row);
                result.Add(entity);
            }
            return result;
        }

        /// <summary>
        /// Reads data reader into a list of entities
        /// </summary>
        /// <param name="dataReader">The data reader.</param>
        /// <returns>A list of the specified entities</returns>
        public static TEntity FillSingle<TEntity>(IDataReader dataReader) where TEntity : new() //must have public parameterless constructor
        {
            int filedcount = dataReader.FieldCount;
            if (filedcount == 0) throw new ArgumentNullException("None of filed be queried");
            ConcurrentDictionary<string, object> row = new ConcurrentDictionary<string, object>();
            if (dataReader.Read())
            {
                row = ReadRow(dataReader, 0, filedcount);
            }
            //close the dataReader
            dataReader.Close();
            if (row.Count == 0)
            {
                return default(TEntity);
            }
            //use the list of dictionaries
            return BuildEntity<TEntity>(row);
        }


        /// <summary>
        /// Reads data reader into a list of entities
        /// </summary>
        /// <param name="dataReader">The data reader.</param>
        /// <returns>A list of the specified entities</returns>
        public static IList<TEntity> Fill<TEntity, TEntityDetail>(IDataReader dataReader, string SplitOn, Func<TEntity, TEntityDetail, TEntity> Func) where TEntity : new()
            where TEntityDetail : new()//must have public parameterless constructor
        {
            int filedcount = dataReader.FieldCount;
            if (filedcount == 0) throw new ArgumentNullException("None of filed be queried");
            var result = new List<TEntity>();
            //a list of dictionaries for each row
            int startBound = dataReader.FieldCount;

            if (SplitOn != null)
            {
                for (int i = 0; i < dataReader.FieldCount; i++)
                {
                    if (dataReader.GetName(i).Equals(SplitOn) && i > 0)
                    {
                        startBound = i;
                        break;
                    }
                }
            }

            while (dataReader.Read())
            {
                var dr = ReadRow(dataReader, 0, startBound);
                var entity = BuildEntity<TEntity>(dr);

                var drdetail = ReadRow(dataReader, startBound, filedcount);
                var entitydetail = BuildEntity<TEntityDetail>(drdetail);
                Func(entity, entitydetail);
            }
            //close the dataReader
            dataReader.Close();
            return result;
        }

        private static ConcurrentDictionary<string, object> ReadRow(IDataRecord record, int startbound = 0, int length = 0)
        {
            var row = new ConcurrentDictionary<string, object>();
            for (int i = startbound; i < length; i++)
            {
                row.GetOrAdd(record.GetName(i), record.GetValue(i));
            }
            return row;
        }

        private static TEntity BuildEntity<TEntity>(IDictionary<string, object> row) where TEntity : new()
        {
            var entity = new TEntity();
            var type = typeof(TEntity);
            foreach (var item in row)
            {
                var propertykey = item.Key;
                var value = item.Value;
                if (value == DBNull.Value) value = null; //may be DBNull
                SetProperty(propertykey, type, entity, value);
            }
            return entity;
        }

        #region Reflect into entity
        private static void SetProperty<TEntity>(string propertykey, Type type, TEntity entity, object value) where TEntity : new()
        {
            //Cache class property
            ConcurrentDictionary<string, MemberInfo> _properties = classProperty.GetOrAdd(type, (key) =>
            {
                return new ConcurrentDictionary<string, MemberInfo>();
            });
            //first try dictionary
            _properties.GetOrAdd(propertykey, (memberkey) =>
            {
                //otherwise (should be first time only) reflect it out
                //first look for a writeable public property of any case
                MemberInfo refmemberinfo = null;
                var property = type.GetProperty(propertykey,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property != null && property.CanWrite)
                {
                    refmemberinfo = property;
                }
                //look for a nonpublic field with the standard _ prefix
                var field = type.GetField(propertykey,
                     BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (field.IsNotNull())
                {
                    refmemberinfo = field;
                }
                return refmemberinfo;
            });
            MemberInfo memberinfo = null;
            if (_properties.TryGetValue(propertykey, out memberinfo))
            {
                SetPropertyFromDictionary(memberinfo, entity, value);
            }
        }

        private static void SetPropertyFromDictionary<TEntity>(MemberInfo member, TEntity entity, object value) where TEntity : new()
        {
            if (value.IsNull())
            {
                return;
            }
            var property = member as PropertyInfo;
            if (property != null)
            {
                var type = GetNullAbleProperty(property.PropertyType);
                var targetvalue = Convert.ChangeType(value, type);
                property.SetValue(entity, targetvalue, null);
            }
            var field = member as FieldInfo;
            if (field != null)
            {
                var targetvalue = Convert.ChangeType(value, field.FieldType);
                field.SetValue(entity, targetvalue);
            }
        }

        private static Type GetNullAbleProperty(Type propertyType)
        {

            var type = propertyType;
            //判断type类型是否为泛型，因为nullable是泛型类,  
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))//判断convertsionType是否为nullable泛型类  
            {
                //如果type为nullable类，声明一个NullableConverter类，该类提供从Nullable类到基础基元类型的转换  
                System.ComponentModel.NullableConverter nullableConverter = new System.ComponentModel.NullableConverter(propertyType);
                //将type转换为nullable对的基础基元类型  
                type = nullableConverter.UnderlyingType;
            }
            return type;
        }
        #endregion

        #region basic data type
        /// <summary>
        /// 获取string集合
        /// </summary>
        /// <param name="dataReader"></param>
        /// <returns></returns>
        public static IList<string> Fill(IDataReader dataReader)
        {
            if (dataReader.FieldCount == 0) throw new ArgumentNullException("None of filed be queried");
            var result = new BlockingCollection<string>();
            while (dataReader.Read())
            {
                result.TryAdd(dataReader.GetValue(0).ToString());
            }
            dataReader.Close();
            return result.ToList();
        }
        #endregion
    }
}
